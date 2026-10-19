// app/api/ingest/route.ts
import { indexConfig } from '@/constants/graphConfigs';
import { fetchKnowledgeAccess } from '@/lib/knowledge-access';
import { langGraphServerClient } from '@/lib/langgraph-server';
import { processPDF } from '@/lib/pdf';
import { Document } from '@langchain/core/documents';
import { NextRequest, NextResponse } from 'next/server';

// Configuration constants
const MAX_FILE_SIZE = 10 * 1024 * 1024; // 10MB
const ALLOWED_FILE_TYPES = ['application/pdf'];

/** Default scope for uploads when the caller may ingest but picks nothing. */
const DEFAULT_SCOPE = 'all';

/**
 * Upload PDFs into the shared knowledge base.
 *
 * Every upload must carry a data ticket, and the WebAdmin decides from that ticket whether the
 * caller may ingest at all and which scopes they may stamp. Without this, anyone who could
 * reach the address could plant documents that every role would then be told are policy — the
 * knowledge base is shared state, so writing to it is a privileged action.
 */
export async function POST(request: NextRequest) {
  try {
    if (!process.env.LANGGRAPH_INGESTION_ASSISTANT_ID) {
      return NextResponse.json(
        {
          error:
            'LANGGRAPH_INGESTION_ASSISTANT_ID is not set in your environment variables',
        },
        { status: 500 },
      );
    }

    const formData = await request.formData();

    // The ticket arrives as a form field rather than a header so the browser can send it with
    // the multipart body it is already building. It is not logged and not part of any URL.
    const ticket = formData.get('ticket');
    const access = await fetchKnowledgeAccess(
      typeof ticket === 'string' ? ticket : undefined,
    );

    if (!access.canIngest) {
      return NextResponse.json(
        {
          error:
            'Tài khoản của bạn không có quyền nạp tài liệu vào kho tri thức. Vui lòng liên hệ Nhân sự hoặc Quản trị viên.',
        },
        { status: 403 },
      );
    }

    // Requested scope is validated against the list the WebAdmin built from the department
    // catalogue, never trusted as-is: an arbitrary string would otherwise be stamped onto the
    // document and become a filter value nobody can match, silently hiding the file forever.
    const requestedScope = formData.get('scope');
    const scope =
      typeof requestedScope === 'string' &&
      access.ingestScopes.includes(requestedScope)
        ? requestedScope
        : DEFAULT_SCOPE;

    const files: File[] = [];

    for (const [key, value] of formData.entries()) {
      if (key === 'files' && value instanceof File) {
        files.push(value);
      }
    }

    if (!files || files.length === 0) {
      return NextResponse.json({ error: 'No files provided' }, { status: 400 });
    }

    // Validate file count
    if (files.length > 5) {
      return NextResponse.json(
        { error: 'Too many files. Maximum 5 files allowed.' },
        { status: 400 },
      );
    }

    // Validate file types and sizes
    const invalidFiles = files.filter((file) => {
      return (
        !ALLOWED_FILE_TYPES.includes(file.type) || file.size > MAX_FILE_SIZE
      );
    });

    if (invalidFiles.length > 0) {
      return NextResponse.json(
        {
          error:
            'Only PDF files are allowed and file size must be less than 10MB',
        },
        { status: 400 },
      );
    }

    // Process all PDFs into Documents, stamping the server-decided scope on every chunk.
    // The stamp happens here rather than in the browser so a client cannot label its upload
    // as belonging to a department it has no business writing to.
    const uploadedAt = new Date().toISOString();
    const allDocs: Document[] = [];
    for (const file of files) {
      try {
        const docs = await processPDF(file);
        docs.forEach((doc) => {
          doc.metadata.scope = scope;
          doc.metadata.uploadedAt = uploadedAt;
          if (access.accountId !== null) {
            doc.metadata.uploadedBy = access.accountId;
          }
        });
        allDocs.push(...docs);
      } catch (error: any) {
        console.error(`Error processing file ${file.name}:`, error);
        // Continue processing other files; errors are logged
      }
    }

    if (!allDocs.length) {
      return NextResponse.json(
        { error: 'No valid documents extracted from uploaded files' },
        { status: 500 },
      );
    }

    // Run the ingestion graph. The assistant id comes from configuration rather than a string
    // literal: the earlier version validated the environment variable and then ignored it,
    // so renaming the graph in langgraph.json would have left this route calling a name that
    // no longer existed while the startup check still passed.
    const thread = await langGraphServerClient.createThread();
    await langGraphServerClient.client.runs.wait(
      thread.thread_id,
      process.env.LANGGRAPH_INGESTION_ASSISTANT_ID,
      {
        input: {
          docs: allDocs,
        },
        config: {
          configurable: {
            ...indexConfig,
          },
        },
      },
    );

    return NextResponse.json({
      message: 'Documents ingested successfully',
      threadId: thread.thread_id,
      // Echoed back so the UI can confirm which scope the files landed in. It is the value
      // the server actually used, not necessarily the one requested.
      scope,
    });
  } catch (error: any) {
    console.error('Error processing files:', error);
    return NextResponse.json(
      { error: 'Failed to process files', details: error.message },
      { status: 500 },
    );
  }
}
