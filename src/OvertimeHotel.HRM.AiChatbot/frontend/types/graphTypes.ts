import { Document } from '@langchain/core/documents';

/**
 * Represents the state of the retrieval graph / agent.
 */
export type documentType =
  | PDFDocument[]
  | { [key: string]: any }[]
  | string[]
  | string
  | 'delete';
export interface AgentState {
  query?: string;
  route?: string;
  messages: Array<{
    content: string;
    additional_kwargs: Record<string, any>;
    response_metadata: Record<string, any>;
    id: string;
    type: 'human' | 'assistant';
  }>;
  documents: documentType;
}

export interface RetrieveDocumentsNodeUpdates {
  retrieveDocuments: {
    documents: documentType;
  };
}

export type PDFDocument = Document & {
  metadata?: {
    loc?: {
      lines?: {
        from: number;
        to: number;
      };
      pageNumber?: number;
    };
    pdf?: {
      info?: {
        Title?: string;
        Creator?: string;
        Producer?: string;
        CreationDate?: string;
        IsXFAPresent?: boolean;
        PDFFormatVersion?: string;
        IsAcroFormPresent?: boolean;
      };
      version?: string;
      metadata?: any;
      totalPages?: number;
    };
    uuid?: string;
    source?: string;
  };
};

export interface BaseConfiguration {
  /**
   * The vector store provider to use for retrieval.
   * @default 'supabase'
   */
  retrieverProvider?: 'supabase';

  /**
   * Additional keyword arguments to pass to the search function of the retriever for filtering.
   * @default {}
   */
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  filterKwargs?: Record<string, any>;

  /**
   * The number of documents to retrieve.
   * @default 5
   */
  k?: number;

  /**
   * Document scopes the caller may read, resolved by the WebAdmin from the ticket.
   *
   * Values are `all`, `dept:<id>` or `*`. Set by the chat route, never read from the browser:
   * the route asks the WebAdmin and overwrites whatever arrived in the request body.
   */
  docScopes?: string[];
}

export interface AgentConfiguration extends BaseConfiguration {
  // models
  /**
   * The language model used for processing and refining queries.
   * Should be in the form: provider/model-name.
   */
  queryModel?: string;

  /**
   * Short-lived data ticket issued by the WebAdmin.
   *
   * Opaque here on purpose — this app never verifies or decodes it, it only relays it to the
   * graph, which sends it back to the WebAdmin. Keeping it opaque avoids a JWT dependency in
   * the Edge runtime and guarantees the token cannot be tampered with in transit.
   */
  accessToken?: string;

  /**
   * Which admin screen the user is looking at, forwarded so the assistant can resolve
   * questions like "what does this number mean?" against the report actually on screen.
   */
  pageContext?: string;

  /**
   * The report period on screen, as raw numbers.
   *
   * The backend already reads these to rebuild the same period at the data bridge; declaring
   * them here keeps the frontend from having to send untyped values. Absent means "the page
   * shows no period", which the bridge treats as its own default.
   */
  year?: number;
  month?: number;
  quarter?: number;
}

export interface IndexConfiguration extends BaseConfiguration {
  /**
   * Path to a JSON file containing default documents to index.
   */
  docsFile?: string;

  /**
   * Whether to use sample documents for indexing.
   */
  useSampleDocs?: boolean;
}
