import { Annotation, MessagesAnnotation } from '@langchain/langgraph';
import { reduceDocs } from '../shared/state.js';
import { Document } from '@langchain/core/documents';
/**
 * Represents the state of the retrieval graph / agent.
 */
export const AgentStateAnnotation = Annotation.Root({
  query: Annotation<string>(),
  route: Annotation<string>(),
  ...MessagesAnnotation.spec,

  /**
   * Populated by the retriever. This is a list of documents that the agent can reference.
   * @type {Document[]}
   */
  documents: Annotation<
    Document[],
    Document[] | { [key: string]: any }[] | string[] | string | 'delete'
  >({
    default: () => [],
    // @ts-ignore
    reducer: reduceDocs,
  }),

  /**
   * Compact JSON digest of report figures fetched from the WebAdmin data bridge.
   *
   * Empty string means "no figures available" — either the route was not taken, the
   * WebAdmin refused the ticket (403), or it was unreachable. The answer node treats an
   * empty digest as "cannot read data right now" and must never invent numbers to fill it.
   */
  dataDigest: Annotation<string>(),

  /**
   * Human-readable reason why `dataDigest` is empty.
   *
   * Kept apart from the digest so the answer node can tell "no figures because the user's role
   * is not allowed" from "no figures because the WebAdmin is down", and word each case
   * correctly without leaking a raw HTTP status into the chat.
   */
  dataError: Annotation<string>(),

  /**
   * Human-readable data scope the WebAdmin resolved for this user, e.g. "Toàn khách sạn"
   * or "Bộ phận: Lễ tân".
   *
   * Surfaced separately from the digest so the chat frame can display which slice of data the
   * answer is based on — the user should be able to see that a manager's figures cover only
   * their own department, without having to read the whole answer.
   */
  scopeLabel: Annotation<string>(),

  /**
   * Person's name extracted by the router when the question routes to `directory`, e.g. "Sang"
   * from "có nhân viên nào tên Sang không?".
   *
   * Only ever narrows a search on the WebAdmin side; the permission scope is still re-resolved
   * there from the ticket, so a wrong or missing keyword can degrade the answer, never widen it.
   * Empty for every other route.
   */
  searchKeyword: Annotation<string>(),
});
