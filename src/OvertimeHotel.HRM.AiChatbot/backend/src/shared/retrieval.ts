import { VectorStoreRetriever } from '@langchain/core/vectorstores';
import { OpenAIEmbeddings } from '@langchain/openai';
import { SupabaseVectorStore } from '@langchain/community/vectorstores/supabase';
import { MemoryVectorStore } from 'langchain/vectorstores/memory';
import { createClient } from '@supabase/supabase-js';
import { RunnableConfig } from '@langchain/core/runnables';
import { BaseRetriever } from '@langchain/core/retrievers';
import { Document } from '@langchain/core/documents';
import {
  BaseConfigurationAnnotation,
  ensureBaseConfiguration,
} from './configuration.js';

let sharedMemoryStore: MemoryVectorStore | null = null;

/** Scope code meaning "the whole knowledge base". Only ever produced by the WebAdmin. */
const WHOLE_KNOWLEDGE_BASE = '*';

/** Metadata key stamped on every uploaded document. Mirrors the C# KnowledgeAccessPolicy. */
const SCOPE_METADATA_KEY = 'scope';

export function getEmbeddings(): OpenAIEmbeddings {
  const apiKey = process.env.AI_API_KEY || process.env.OPENAI_API_KEY;
  const baseURL = process.env.AI_BASE_URL || process.env.OPENAI_BASE_URL;
  const model = process.env.EMBEDDING_MODEL || 'text-embedding-3-small';

  return new OpenAIEmbeddings({
    model,
    apiKey,
    configuration: baseURL ? { baseURL } : undefined,
  });
}

export async function makeMemoryRetriever(
  configuration: typeof BaseConfigurationAnnotation.State,
  scopeFilter: Record<string, unknown> = {},
): Promise<VectorStoreRetriever> {
  const embeddings = getEmbeddings();
  if (!sharedMemoryStore) {
    sharedMemoryStore = new MemoryVectorStore(embeddings);
  }

  // `scope` is merged in by the caller for per-department searches; anything already in
  // filterKwargs is kept so older configurations keep working unchanged.
  const effectiveFilter = { ...configuration.filterKwargs, ...scopeFilter };
  const hasFilter = Object.keys(effectiveFilter).length > 0;

  return sharedMemoryStore.asRetriever({
    k: configuration.k,
    filter: hasFilter
      ? (doc) => {
          for (const [key, val] of Object.entries(effectiveFilter)) {
            if (doc.metadata?.[key] !== val) return false;
          }
          return true;
        }
      : undefined,
  });
}

export async function makeSupabaseRetriever(
  configuration: typeof BaseConfigurationAnnotation.State,
  scopeFilter: Record<string, unknown> = {},
): Promise<VectorStoreRetriever> {
  if (!process.env.SUPABASE_URL || !process.env.SUPABASE_SERVICE_ROLE_KEY) {
    console.warn(
      'SUPABASE_URL or SUPABASE_SERVICE_ROLE_KEY not configured. Falling back to in-memory vector store.',
    );
    return makeMemoryRetriever(configuration, scopeFilter);
  }
  const embeddings = getEmbeddings();
  const supabaseClient = createClient(
    process.env.SUPABASE_URL ?? '',
    process.env.SUPABASE_SERVICE_ROLE_KEY ?? '',
  );
  const vectorStore = new SupabaseVectorStore(embeddings, {
    client: supabaseClient,
    tableName: 'documents',
    queryName: 'match_documents',
  });

  const effectiveFilter = { ...configuration.filterKwargs, ...scopeFilter };

  return vectorStore.asRetriever({
    k: configuration.k,
    // The RPC applies this as `metadata @> filter` (jsonb containment), so a plain object is
    // handed straight through. When empty, undefined leaves the search unfiltered.
    filter: Object.keys(effectiveFilter).length > 0 ? effectiveFilter : undefined,
  });
}

async function makeSingleRetriever(
  config: RunnableConfig,
  scopeFilter: Record<string, unknown>,
): Promise<VectorStoreRetriever> {
  const configuration = ensureBaseConfiguration(config);
  switch (configuration.retrieverProvider) {
    case 'supabase':
      return makeSupabaseRetriever(configuration, scopeFilter);
    case 'memory':
      return makeMemoryRetriever(configuration, scopeFilter);
    default:
      if (!process.env.SUPABASE_URL) {
        return makeMemoryRetriever(configuration, scopeFilter);
      }
      throw new Error(
        `Unsupported retriever provider: ${configuration.retrieverProvider}`,
      );
  }
}

/** Constant from the reciprocal-rank-fusion literature; dampens the influence of top ranks. */
const RRF_K = 60;

/**
 * Merge several ranked document lists into one, scoring each document by reciprocal rank.
 *
 * Documents are identified by `metadata.uuid` (set by the Supabase vector store), falling back
 * to the page content. Without a stable key the same chunk returned by two searches would be
 * counted twice and crowd out other, genuinely different chunks.
 */
function fuseRankedLists(lists: Document[][]): Document[] {
  const scores = new Map<string, { doc: Document; score: number }>();

  for (const list of lists) {
    list.forEach((doc, index) => {
      const key =
        typeof doc.metadata?.uuid === 'string'
          ? doc.metadata.uuid
          : doc.pageContent;

      const existing = scores.get(key);
      const increment = 1 / (RRF_K + index + 1);

      if (existing) {
        existing.score += increment;
      } else {
        scores.set(key, { doc, score: increment });
      }
    });
  }

  return [...scores.values()]
    .sort((a, b) => b.score - a.score)
    .map((entry) => entry.doc);
}

/**
 * Retriever combining one filtered search per document scope the caller may read.
 *
 * Why a merged search rather than one query with an `$in` filter: `match_documents` accepts a
 * single jsonb containment object, and the scope list is a union of unrelated codes
 * (`all` plus one department). Asking once per scope and merging is the only way to express
 * that union without changing a database function every member has already installed.
 */
class ScopedRetriever extends BaseRetriever {
  lc_namespace = ['overtime', 'retrievers', 'scoped'];

  constructor(
    private readonly retrievers: BaseRetriever[],
    private readonly topK: number,
  ) {
    super();
  }

  async _getRelevantDocuments(query: string): Promise<Document[]> {
    if (this.retrievers.length === 0) {
      return [];
    }

    if (this.retrievers.length === 1) {
      return this.retrievers[0].invoke(query);
    }

    const rankedLists = await Promise.all(
      this.retrievers.map((retriever) => retriever.invoke(query)),
    );

    return fuseRankedLists(rankedLists).slice(0, this.topK);
  }
}

/**
 * Retriever used to WRITE documents, for the ingestion graph only.
 *
 * Kept separate from {@link makeRetriever} because writing needs `addDocuments`, which only the
 * concrete vector-store retriever exposes — the merged, read-only retriever deliberately does
 * not, so a retrieval path cannot be handed to an ingest path by mistake. Ingestion is not
 * scope-filtered: the scope is stamped onto each document's metadata before it is stored.
 */
export async function makeIndexingRetriever(
  config: RunnableConfig,
): Promise<VectorStoreRetriever> {
  return makeSingleRetriever(config, {});
}

/**
 * Build the retriever for one chat turn.
 *
 * The scope list comes from `configuration.docScopes`, which the chat route fills from the
 * WebAdmin's answer for the signed-in user. `*` short-circuits to an unfiltered search;
 * otherwise each scope becomes its own filtered search.
 */
export async function makeRetriever(
  config: RunnableConfig,
): Promise<BaseRetriever> {
  const configuration = ensureBaseConfiguration(config);
  const scopes = configuration.docScopes;

  if (scopes.includes(WHOLE_KNOWLEDGE_BASE)) {
    // Privileged roles read the whole knowledge base, including documents stamped with a
    // department scope. Running one search per known department would miss any scope added
    // later, so the unfiltered form is both simpler and more correct.
    return makeSingleRetriever(config, {});
  }

  const retrievers = await Promise.all(
    scopes.map((scope) =>
      makeSingleRetriever(config, { [SCOPE_METADATA_KEY]: scope }),
    ),
  );

  return new ScopedRetriever(retrievers, configuration.k);
}
