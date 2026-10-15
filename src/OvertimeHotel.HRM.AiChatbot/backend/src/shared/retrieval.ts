import { VectorStoreRetriever } from '@langchain/core/vectorstores';
import { OpenAIEmbeddings } from '@langchain/openai';
import { SupabaseVectorStore } from '@langchain/community/vectorstores/supabase';
import { MemoryVectorStore } from 'langchain/vectorstores/memory';
import { createClient } from '@supabase/supabase-js';
import { RunnableConfig } from '@langchain/core/runnables';
import {
  BaseConfigurationAnnotation,
  ensureBaseConfiguration,
} from './configuration.js';

let sharedMemoryStore: MemoryVectorStore | null = null;

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
): Promise<VectorStoreRetriever> {
  const embeddings = getEmbeddings();
  if (!sharedMemoryStore) {
    sharedMemoryStore = new MemoryVectorStore(embeddings);
  }
  const hasFilter =
    configuration.filterKwargs &&
    Object.keys(configuration.filterKwargs).length > 0;

  return sharedMemoryStore.asRetriever({
    k: configuration.k,
    filter: hasFilter
      ? (doc) => {
          for (const [key, val] of Object.entries(configuration.filterKwargs)) {
            if (doc.metadata?.[key] !== val) return false;
          }
          return true;
        }
      : undefined,
  });
}

export async function makeSupabaseRetriever(
  configuration: typeof BaseConfigurationAnnotation.State,
): Promise<VectorStoreRetriever> {
  if (!process.env.SUPABASE_URL || !process.env.SUPABASE_SERVICE_ROLE_KEY) {
    console.warn(
      'SUPABASE_URL or SUPABASE_SERVICE_ROLE_KEY not configured. Falling back to in-memory vector store.',
    );
    return makeMemoryRetriever(configuration);
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
  return vectorStore.asRetriever({
    k: configuration.k,
    filter: configuration.filterKwargs,
  });
}

export async function makeRetriever(
  config: RunnableConfig,
): Promise<VectorStoreRetriever> {
  const configuration = ensureBaseConfiguration(config);
  switch (configuration.retrieverProvider) {
    case 'supabase':
      return makeSupabaseRetriever(configuration);
    case 'memory':
      return makeMemoryRetriever(configuration);
    default:
      if (!process.env.SUPABASE_URL) {
        return makeMemoryRetriever(configuration);
      }
      throw new Error(
        `Unsupported retriever provider: ${configuration.retrieverProvider}`,
      );
  }
}
