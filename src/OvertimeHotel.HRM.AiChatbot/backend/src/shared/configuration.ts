/**
 * Define the configurable parameters for the agent.
 */

import { Annotation } from '@langchain/langgraph';
import { RunnableConfig } from '@langchain/core/runnables';

/**
 * typeof ConfigurationAnnotation.State class for indexing and retrieval operations.
 *
 * This annotation defines the parameters needed for configuring the indexing and
 * retrieval processes, including user identification, embedding model selection,
 * retriever provider choice, and search parameters.
 */
export const BaseConfigurationAnnotation = Annotation.Root({
  /**
   * The vector store provider to use for retrieval.
   * Options are 'supabase' or 'memory' (in-memory fallback)
   */
  retrieverProvider: Annotation<'supabase' | 'memory'>,

  /**
   * Additional keyword arguments to pass to the search function of the retriever for filtering.
   */
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  filterKwargs: Annotation<Record<string, any>>,

  /**
   * The number of documents to retrieve.
   */
  k: Annotation<number>,

  /**
   * Which document scopes this caller may read.
   *
   * Resolved by the WebAdmin from the signed-in user's role and department — never taken from
   * the browser. Values are `all` (shared documents), `dept:<id>` (one department) or `*`
   * (the whole knowledge base). Retrieval runs one filtered search per scope and merges the
   * results, so a manager sees shared policy plus their own department without seeing others.
   */
  docScopes: Annotation<string[]>,
});

/**
 * Create an typeof BaseConfigurationAnnotation.State instance from a RunnableConfig object.
 *
 * @param config - The configuration object to use.
 * @returns An instance of typeof BaseConfigurationAnnotation.State with the specified configuration.
 */
export function ensureBaseConfiguration(
  config: RunnableConfig,
): typeof BaseConfigurationAnnotation.State {
  const configurable = (config?.configurable || {}) as Partial<
    typeof BaseConfigurationAnnotation.State
  >;
  const defaultProvider =
    process.env.SUPABASE_URL && process.env.SUPABASE_SERVICE_ROLE_KEY
      ? 'supabase'
      : 'memory';
  return {
    retrieverProvider: configurable.retrieverProvider || defaultProvider,
    filterKwargs: configurable.filterKwargs || {},
    k: configurable.k || 5,
    docScopes: normalizeDocScopes(configurable.docScopes),
  };
}

/**
 * Accept only well-formed scope codes, and fall back to the narrowest one.
 *
 * The list normally comes from the WebAdmin, but the graph is also invoked directly in tests
 * and demos, so it is still treated as untrusted. An unrecognised value is dropped rather than
 * forwarded: a typo becoming a filter that matches nothing is a silently empty answer, while a
 * typo passed through could reach the database function as an unexpected argument.
 *
 * An empty result means "shared documents only" — never "everything". Failing open here would
 * turn a missing scope into a knowledge-base-wide read.
 */
function normalizeDocScopes(value: unknown): string[] {
  if (!Array.isArray(value)) {
    return ['all'];
  }

  const scopes = value.filter(
    (item): item is string =>
      typeof item === 'string' &&
      (item === '*' || item === 'all' || /^dept:\d+$/.test(item)),
  );

  return scopes.length > 0 ? Array.from(new Set(scopes)) : ['all'];
}
