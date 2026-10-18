import { Annotation } from '@langchain/langgraph';
import { RunnableConfig } from '@langchain/core/runnables';
import {
  BaseConfigurationAnnotation,
  ensureBaseConfiguration,
} from '../shared/configuration.js';

/**
 * The configuration for the agent.
 */
export const AgentConfigurationAnnotation = Annotation.Root({
  ...BaseConfigurationAnnotation.spec,

  // models
  /**
   * The language model used for processing and refining queries.
   * Should be in the form: provider/model-name.
   */
  queryModel: Annotation<string>,

  /**
   * Short-lived data ticket issued by the WebAdmin, carried in the iframe URL fragment.
   *
   * Opaque to this service: only the WebAdmin can verify it. It proves *who* is asking —
   * never *what they may see*, which the WebAdmin re-resolves from its own RBAC. Empty for
   * anonymous embedding (e.g. the chatbot opened outside the admin portal).
   */
  accessToken: Annotation<string>,

  /**
   * Which admin screen the user is looking at, e.g. `BaoCao:Index:quy-luong:Tháng 10/2026`.
   *
   * Purely contextual — it lets the assistant resolve "what does this number mean?" against
   * the report actually on screen. It carries no authority: a forged value can only mislead
   * the wording, never widen the data scope, which the WebAdmin decides on its own.
   */
  pageContext: Annotation<string>,

  /**
   * The report period the user is looking at, as raw numbers rather than a label.
   *
   * Separate from {@link pageContext} because that one is prose for the model to read. These
   * are sent back to the WebAdmin to rebuild the *same* period; a label such as "Tháng 10"
   * cannot be turned back into a query without guessing. They narrow which figures are
   * fetched, never which figures are permitted — that stays with the WebAdmin.
   */
  year: Annotation<number>,
  month: Annotation<number>,
  quarter: Annotation<number>,
});

/**
 * Create a typeof ConfigurationAnnotation.State instance from a RunnableConfig object.
 *
 * @param config - The configuration object to use.
 * @returns An instance of typeof ConfigurationAnnotation.State with the specified configuration.
 */
export function ensureAgentConfiguration(
  config: RunnableConfig,
): typeof AgentConfigurationAnnotation.State {
  const configurable = (config?.configurable || {}) as Partial<
    typeof AgentConfigurationAnnotation.State
  >;
  const baseConfig = ensureBaseConfiguration(config);
  return {
    ...baseConfig,
    queryModel: configurable.queryModel || 'openai/gpt-4o',
    accessToken: configurable.accessToken || '',
    pageContext: configurable.pageContext || '',
    // Zero means "not supplied" — no calendar has a year 0, month 0 or quarter 0, and the
    // digest endpoint treats a missing value as "use its own default for the period".
    year: toPositiveInteger(configurable.year),
    month: toPositiveInteger(configurable.month),
    quarter: toPositiveInteger(configurable.quarter),
  };
}

/**
 * Accept only a whole, positive number from the request-supplied configuration.
 *
 * The values arrive from the browser, so they are untrusted input even though they only ever
 * narrow a report period. Returns 0 for anything else, which downstream reads as "absent".
 */
function toPositiveInteger(value: unknown): number {
  const parsed = typeof value === 'number' ? value : Number(value);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : 0;
}
