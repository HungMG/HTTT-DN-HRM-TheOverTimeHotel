import { StateGraph, START, END } from '@langchain/langgraph';
import { AgentStateAnnotation } from './state.js';
import { makeRetriever } from '../shared/retrieval.js';
import { formatDocs } from './utils.js';
import { HumanMessage } from '@langchain/core/messages';
import { z } from 'zod';
import {
  DATA_SYSTEM_PROMPT,
  DIRECT_SYSTEM_PROMPT,
  RESPONSE_SYSTEM_PROMPT,
  ROUTER_SYSTEM_PROMPT,
} from './prompts.js';
import { RunnableConfig } from '@langchain/core/runnables';
import {
  AgentConfigurationAnnotation,
  ensureAgentConfiguration,
} from './configuration.js';
import { loadChatModel } from '../shared/utils.js';

async function checkQueryType(
  state: typeof AgentStateAnnotation.State,
  config: RunnableConfig,
): Promise<{
  route: 'retrieve' | 'direct' | 'data' | 'payslip';
}> {
  //schema for routing
  const schema = z.object({
    // `payslip` shares the data bridge node with `data`; it only changes which slice of
    // figures the WebAdmin is asked for. Kept as its own route because the two are told
    // apart by what the user is asking about, not by anything the graph could infer later.
    route: z.enum(['retrieve', 'direct', 'data', 'payslip']),
  });

  const configuration = ensureAgentConfiguration(config);
  const model = await loadChatModel(configuration.queryModel);

  const routingPrompt = ROUTER_SYSTEM_PROMPT;

  const formattedPrompt = await routingPrompt.invoke({
    query: state.query,
  });

  try {
    const response = await model
      .withStructuredOutput(schema)
      .invoke(formattedPrompt.toString());

    const route = response.route;
    return { route };
  } catch (err) {
    console.warn(
      'Structured output routing failed, defaulting to retrieve:',
      err,
    );
    return { route: 'retrieve' };
  }
}

async function answerQueryDirectly(
  state: typeof AgentStateAnnotation.State,
  config: RunnableConfig,
): Promise<typeof AgentStateAnnotation.Update> {
  const configuration = ensureAgentConfiguration(config);
  const model = await loadChatModel(configuration.queryModel);
  const userHumanMessage = new HumanMessage(state.query);

  const formattedPrompt = await DIRECT_SYSTEM_PROMPT.invoke({});
  const systemMessages = formattedPrompt.toChatMessages();

  const previousMessages = (state.messages || []).filter(
    (m) => m.content !== state.query,
  );
  const messageHistory = [
    ...systemMessages,
    ...previousMessages,
    userHumanMessage,
  ];

  const response = await model.invoke(messageHistory);
  return { messages: [userHumanMessage, response] };
}

async function routeQuery(
  state: typeof AgentStateAnnotation.State,
): Promise<'retrieveDocuments' | 'directAnswer' | 'loadDataDigest'> {
  const route = state.route;
  if (!route) {
    throw new Error('Route is not set');
  }

  if (route === 'retrieve') {
    return 'retrieveDocuments';
  } else if (route === 'direct') {
    return 'directAnswer';
  } else if (route === 'data' || route === 'payslip') {
    // Same node: the scope travels on state, and the WebAdmin decides what each one returns.
    return 'loadDataDigest';
  } else {
    throw new Error('Invalid route');
  }
}

async function retrieveDocuments(
  state: typeof AgentStateAnnotation.State,
  config: RunnableConfig,
): Promise<typeof AgentStateAnnotation.Update> {
  const retriever = await makeRetriever(config);
  const response = await retriever.invoke(state.query);

  return { documents: response };
}

async function generateResponse(
  state: typeof AgentStateAnnotation.State,
  config: RunnableConfig,
): Promise<typeof AgentStateAnnotation.Update> {
  const configuration = ensureAgentConfiguration(config);
  const context = formatDocs(state.documents);
  const model = await loadChatModel(configuration.queryModel);
  const promptTemplate = RESPONSE_SYSTEM_PROMPT;

  const formattedPrompt = await promptTemplate.invoke({
    question: state.query,
    context: context,
  });

  const userHumanMessage = new HumanMessage(state.query);
  const systemMessages = formattedPrompt.toChatMessages();

  const previousMessages = (state.messages || []).filter(
    (m) => m.content !== state.query,
  );

  const messageHistory = [
    ...systemMessages,
    ...previousMessages,
    userHumanMessage,
  ];

  const response = await model.invoke(messageHistory);

  return { messages: [userHumanMessage, response] };
}

/**
 * Failure carrying the HTTP status returned by the WebAdmin data bridge.
 *
 * A local error class rather than a status-carrying error from a library, so the answer node
 * can separate "you are not allowed to see this" (403) from "the service is down" (anything
 * else) without string-matching a message that a future library version may reword.
 */
class DataBridgeError extends Error {
  constructor(readonly status: number) {
    super(`Data bridge responded with status ${status}`);
    this.name = 'DataBridgeError';
  }
}

/**
 * Fetch the compact report digest for the signed-in user.
 *
 * The URL comes from `process.env.DATA_API_URL` and **only** from there. It must never be
 * read off `config.configurable`: that object is supplied by the browser, so honouring it
 * would let a caller point this server-side fetch at an arbitrary host and have the real
 * bearer ticket sent along with it — a server-side request forgery.
 *
 * Any failure leaves the digest empty and records why. The answer node then says figures are
 * unavailable rather than quoting zeros, and the chat stays usable when the WebAdmin is down.
 */
async function loadDataDigest(
  state: typeof AgentStateAnnotation.State,
  config: RunnableConfig,
): Promise<typeof AgentStateAnnotation.Update> {
  const configuration = ensureAgentConfiguration(config);
  const dataApiUrl = process.env.DATA_API_URL;

  // The scope is chosen here from the route the graph itself picked, never from the browser.
  // A caller-supplied scope would let anyone ask for the payroll-report slice by naming it in
  // the request; the WebAdmin would still refuse, but there is no reason to forward the claim.
  const scope = state.route === 'payslip' ? 'my-payslip' : 'reports';

  if (!dataApiUrl || !configuration.accessToken) {
    return {
      dataDigest: '',
      dataError:
        'Thiếu cấu hình kết nối hoặc phiên đăng nhập đã hết hiệu lực, nên chưa lấy được số liệu.',
    };
  }

  try {
    const response = await fetch(`${dataApiUrl}/api/ai-data/digest`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        token: configuration.accessToken,
        scope,
        // The period on screen, so the figures match the page the user is asking about.
        // Omitted when zero: the endpoint then applies its own default rather than receiving
        // a year 0 / month 0 that would silently mean something else.
        ...(configuration.year > 0 ? { year: configuration.year } : {}),
        ...(configuration.month > 0 ? { month: configuration.month } : {}),
        ...(configuration.quarter > 0 ? { quarter: configuration.quarter } : {}),
      }),
      // Without a deadline a hung WebAdmin would hold the whole chat turn open.
      signal: AbortSignal.timeout(8000),
    });

    if (!response.ok) {
      throw new DataBridgeError(response.status);
    }

    const digest = JSON.stringify(await response.json());
    return {
      dataDigest: digest,
      dataError: '',
      // Read here, one step before the answer node, so both the prompt and the chat frame
      // use the same server-resolved label rather than each deriving its own.
      scopeLabel: readDigestLabels(digest).scopeLabel,
    };
  } catch (err) {
    if (err instanceof Error && err.name === 'AbortError') {
      console.warn('Data digest request timed out.');
      return {
        dataDigest: '',
        dataError: 'Hệ thống dữ liệu phản hồi quá chậm, chưa lấy được số liệu.',
      };
    }

    if (err instanceof DataBridgeError) {
      // Only a refusal is expected in normal use: the user's role has no report access.
      // The raw status is logged for operators but never surfaced to the chat.
      console.warn(`Data digest request rejected with status ${err.status}.`);
      return {
        dataDigest: '',
        dataError:
          err.status === 403
            ? 'Vai trò của bạn không được xem số liệu báo cáo, nên tôi chưa có dữ liệu để phân tích.'
            : 'Hệ thống dữ liệu đang bận, chưa lấy được số liệu.',
      };
    }

    console.warn('Data digest request failed:', err);
    return {
      dataDigest: '',
      dataError: 'Chưa kết nối được hệ thống dữ liệu, nên tôi chưa lấy được số liệu.',
    };
  }
}

/**
 * Read the scope and role labels out of the digest JSON.
 *
 * Returns placeholders when the digest is missing or malformed: the labels are decoration for
 * the prompt, and a parse failure here must not abort an answer that would otherwise succeed.
 */
function readDigestLabels(digest: string): {
  scopeLabel: string;
  roleLabel: string;
} {
  const fallback = 'chưa xác định';
  if (!digest) {
    return { scopeLabel: fallback, roleLabel: fallback };
  }

  try {
    const parsed = JSON.parse(digest) as {
      scopeLabel?: string;
      roleLabel?: string;
    };
    return {
      scopeLabel: parsed.scopeLabel || fallback,
      roleLabel: parsed.roleLabel || fallback,
    };
  } catch {
    return { scopeLabel: fallback, roleLabel: fallback };
  }
}

/**
 * Answer using the fetched digest instead of retrieved documents.
 *
 * A separate node rather than a branch inside `generateResponse`: the two prompts take
 * different inputs (figures vs. document excerpts), and keeping them apart means a change to
 * the document answer cannot silently alter how figures are reported.
 */
async function answerWithData(
  state: typeof AgentStateAnnotation.State,
  config: RunnableConfig,
): Promise<typeof AgentStateAnnotation.Update> {
  const configuration = ensureAgentConfiguration(config);
  const model = await loadChatModel(configuration.queryModel);

  // Prefer the digest; fall back to the recorded reason so the model can explain the gap.
  // Without this fallback an empty digest reaches the prompt as silence, and the model fills
  // that silence with invented numbers.
  const context = state.dataDigest || `(không có số liệu) ${state.dataError || ''}`.trim();

  // Scope and role come from the digest itself, which the WebAdmin filled in after resolving
  // the user's permissions. Reading them off `pageContext` would let the browser assert its
  // own scope label, and the model would then repeat a claim the server never made.
  const labels = readDigestLabels(state.dataDigest);

  const formattedPrompt = await DATA_SYSTEM_PROMPT.invoke({
    scope: labels.scopeLabel,
    role: labels.roleLabel,
    pageContext: configuration.pageContext || 'không xác định',
    context,
    question: state.query,
  });

  const userHumanMessage = new HumanMessage(state.query);
  const systemMessages = formattedPrompt.toChatMessages();

  const previousMessages = (state.messages || []).filter(
    (m) => m.content !== state.query,
  );

  const messageHistory = [
    ...systemMessages,
    ...previousMessages,
    userHumanMessage,
  ];

  const response = await model.invoke(messageHistory);

  return { messages: [userHumanMessage, response] };
}

const builder = new StateGraph(
  AgentStateAnnotation,
  AgentConfigurationAnnotation,
)
  .addNode('retrieveDocuments', retrieveDocuments)
  .addNode('generateResponse', generateResponse)
  .addNode('checkQueryType', checkQueryType)
  .addNode('directAnswer', answerQueryDirectly)
  .addNode('loadDataDigest', loadDataDigest)
  .addNode('dataInsight', answerWithData)
  .addEdge(START, 'checkQueryType')
  .addConditionalEdges('checkQueryType', routeQuery, [
    'retrieveDocuments',
    'directAnswer',
    'loadDataDigest',
  ])
  .addEdge('retrieveDocuments', 'generateResponse')
  .addEdge('generateResponse', END)
  .addEdge('loadDataDigest', 'dataInsight')
  .addEdge('dataInsight', END)
  .addEdge('directAnswer', END);

export const graph = builder.compile().withConfig({
  runName: 'RetrievalGraph',
});
