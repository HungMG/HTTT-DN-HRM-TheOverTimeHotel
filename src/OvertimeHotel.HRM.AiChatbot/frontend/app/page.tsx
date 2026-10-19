'use client';

import type React from 'react';

import { useToast } from '@/hooks/use-toast';
import { useRef, useState, useEffect } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import {
  Paperclip,
  ArrowUp,
  Loader2,
  Bot,
  FileText,
  Sparkles,
  UploadCloud,
  RotateCcw,
  CheckCircle2,
} from 'lucide-react';
import { ChatMessage } from '@/components/chat-message';
import { FilePreview } from '@/components/file-preview';
import { client } from '@/lib/langgraph-client';
import {
  PDFDocument,
  RetrieveDocumentsNodeUpdates,
} from '@/types/graphTypes';

const SUGGESTED_PROMPTS = [
  'Tỷ lệ thôi việc tháng này nghĩa là gì?',
  'Cơ cấu nhân lực hiện tại của khách sạn ra sao?',
  'Quy chế phụ cấp ca đêm (+30%) được tính như thế nào?',
  'Công chuẩn hàng tháng và công thức tính lương 2 tầng ra sao?',
];

export default function Home() {
  const { toast } = useToast();
  const [messages, setMessages] = useState<
    Array<{
      role: 'user' | 'assistant';
      content: string;
      sources?: PDFDocument[];
    }>
  >([]);
  const [input, setInput] = useState('');
  const [files, setFiles] = useState<File[]>([]);
  const [isUploading, setIsUploading] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [threadId, setThreadId] = useState<string | null>(null);
  const [isEmbedded, setIsEmbedded] = useState(false);
  const [scopeLabel, setScopeLabel] = useState('');
  const fileInputRef = useRef<HTMLInputElement>(null);
  const abortControllerRef = useRef<AbortController | null>(null);
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const lastRetrievedDocsRef = useRef<PDFDocument[]>([]);
  // Read once at mount and held in refs: the WebAdmin mints a fresh ticket per page load, and
  // re-reading the address on every keystroke would give a rotating value a chance to arrive
  // mid-conversation, pointing the backend at a ticket that is already spent.
  const accessTokenRef = useRef('');
  const pageContextRef = useRef('');
  // The period as raw numbers, kept alongside the human-readable description above. The
  // backend needs numbers to rebuild the same report period; the description is only for the
  // model to read.
  const periodRef = useRef<{ year?: number; month?: number; quarter?: number }>({});

  useEffect(() => {
    if (typeof window !== 'undefined') {
      const params = new URLSearchParams(window.location.search);
      const embedded = window.self !== window.top || params.has('embed');
      setIsEmbedded(embedded);

      // Page context travels in the query string and is safe to keep there.
      const page = params.get('page') ?? '';
      const action = params.get('action') ?? '';
      const tab = params.get('tab') ?? '';
      const period = params.get('period') ?? '';
      pageContextRef.current = [
        page && `màn hình ${page}`,
        action && `chức năng ${action}`,
        tab && `thẻ ${tab}`,
        period && `kỳ ${period}`,
      ]
        .filter(Boolean)
        .join(', ');

      // Parsed separately from the description above so the backend can ask the WebAdmin for
      // that exact period. Absent or malformed values are dropped rather than guessed: a wrong
      // figure presented as the one on screen is worse than saying the period is unknown.
      const toNumber = (key: string) => {
        const raw = params.get(key);
        if (!raw) return undefined;
        const value = Number.parseInt(raw, 10);
        return Number.isFinite(value) ? value : undefined;
      };
      periodRef.current = {
        year: toNumber('nam'),
        month: toNumber('thang'),
        quarter: toNumber('quy'),
      };

      // The data ticket travels in the URL fragment, which browsers never send to a server.
      // It stays out of the Next.js access log and off the Referer header of any later
      // request. Read it once, then strip it so it does not linger in the address bar or in
      // the history entry the user might bookmark or share.
      const hash = window.location.hash;
      if (hash.startsWith('#bt=')) {
        accessTokenRef.current = decodeURIComponent(hash.slice('#bt='.length));
        window.history.replaceState(
          null,
          '',
          window.location.pathname + window.location.search,
        );
      }
    }
  }, []);

  const initThread = async () => {
    try {
      const thread = await client.createThread();
      setThreadId(thread.thread_id);
    } catch (error) {
      console.error('Error creating thread:', error);
      toast({
        title: 'Lỗi khởi tạo Thread',
        description: 'Không thể kết nối đến backend LangGraph (port 2024). Vui lòng thử lại.',
        variant: 'destructive',
      });
    }
  };

  useEffect(() => {
    initThread();
  }, []);

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages, isLoading]);

  const handleResetChat = () => {
    setMessages([]);
    // Clear the scope banner too: it described the previous conversation's figures and would
    // otherwise sit above an empty new chat claiming a scope nothing has been read under.
    setScopeLabel('');
    initThread();
    toast({
      title: 'Đã làm mới cuộc trò chuyện',
      description: 'Phiên trò chuyện mới đã được sẵn sàng.',
    });
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!input.trim() || !threadId || isLoading) return;

    if (abortControllerRef.current) {
      abortControllerRef.current.abort();
    }

    const userMessage = input.trim();
    setMessages((prev) => [
      ...prev,
      { role: 'user', content: userMessage, sources: undefined },
      { role: 'assistant', content: '', sources: undefined },
    ]);
    setInput('');
    setIsLoading(true);

    const abortController = new AbortController();
    abortControllerRef.current = abortController;
    lastRetrievedDocsRef.current = [];

    try {
      const response = await fetch('/api/chat', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          message: userMessage,
          threadId,
          accessToken: accessTokenRef.current || undefined,
          pageContext: pageContextRef.current || undefined,
          year: periodRef.current.year,
          month: periodRef.current.month,
          quarter: periodRef.current.quarter,
        }),
        signal: abortController.signal,
      });

      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }

      const reader = response.body?.getReader();
      if (!reader) throw new Error('No reader available');

      const decoder = new TextDecoder();

      let buffer = '';
      let activeAiMessageId: string | null = null;

      while (true) {
        const { done, value } = await reader.read();
        if (done) break;

        buffer += decoder.decode(value, { stream: true });
        const lines = buffer.split('\n');
        // Keep the trailing un-terminated piece in buffer
        buffer = lines.pop() ?? '';

        for (const rawLine of lines) {
          const line = rawLine.trim();
          if (!line.startsWith('data: ')) continue;

          const sseString = line.slice('data: '.length);
          let sseEvent: any;
          try {
            sseEvent = JSON.parse(sseString);
          } catch (err) {
            console.error('Error parsing SSE line:', err, line);
            continue;
          }

          const { event, data } = sseEvent;

          if (event === 'messages/metadata' && data) {
            const metaKeys = Object.keys(data);
            for (const key of metaKeys) {
              const node = data[key]?.metadata?.langgraph_node;
              // `dataInsight` must be listed here too. Omitting it leaves activeAiMessageId
              // null for data answers, so every later partial chunk is judged by the
              // tool-call heuristic, matches nothing, and the answer never renders.
              if (
                node === 'directAnswer' ||
                node === 'generateResponse' ||
                node === 'dataInsight'
              ) {
                activeAiMessageId = key;
              }
            }
          } else if (event === 'messages/partial') {
            if (Array.isArray(data)) {
              const lastObj = data[data.length - 1];
              if (lastObj?.type === 'ai') {
                // Chỉ nhận chunk từ node trả lời chính (directAnswer / generateResponse), bỏ qua router chunk
                const isTarget = activeAiMessageId
                  ? lastObj.id === activeAiMessageId
                  : !lastObj.tool_calls?.length && !lastObj.tool_call_chunks?.length;

                if (isTarget) {
                  const partialContent = lastObj.content ?? '';

                  if (
                    typeof partialContent === 'string' &&
                    partialContent.length > 0 &&
                    !partialContent.startsWith('{')
                  ) {
                    setMessages((prev) => {
                      const newArr = [...prev];
                      if (
                        newArr.length > 0 &&
                        newArr[newArr.length - 1].role === 'assistant'
                      ) {
                        newArr[newArr.length - 1].content = partialContent;
                        newArr[newArr.length - 1].sources =
                          lastRetrievedDocsRef.current;
                      }
                      return newArr;
                    });
                  }
                }
              }
            }
          } else if (event === 'messages/complete') {
            // Chỉ cập nhật từ complete event nếu khớp đúng message ID của lượt trả lời hiện tại,
            // ngăn chặn lịch sử chat cũ gửi từ backend lúc bắt đầu stream làm nhảy tin nhắn.
            if (Array.isArray(data) && activeAiMessageId) {
              const targetMsg = data.find((m: any) => m?.id === activeAiMessageId);
              if (targetMsg?.content && typeof targetMsg.content === 'string' && !targetMsg.content.startsWith('{')) {
                setMessages((prev) => {
                  const newArr = [...prev];
                  if (
                    newArr.length > 0 &&
                    newArr[newArr.length - 1].role === 'assistant'
                  ) {
                    newArr[newArr.length - 1].content = targetMsg.content;
                    newArr[newArr.length - 1].sources =
                      lastRetrievedDocsRef.current;
                  }
                  return newArr;
                });
              }
            }
          } else if (event === 'updates' && data) {
            if (
              data &&
              typeof data === 'object' &&
              'retrieveDocuments' in data &&
              data.retrieveDocuments &&
              Array.isArray(data.retrieveDocuments.documents)
            ) {
              const retrievedDocs = (data as RetrieveDocumentsNodeUpdates)
                .retrieveDocuments.documents as PDFDocument[];
              lastRetrievedDocsRef.current = retrievedDocs;
            }

            // Carry the scope label the WebAdmin resolved, so the frame can show which slice
            // of data this answer covers. An empty digest leaves it empty — the banner then
            // stays hidden rather than claiming a scope the server never confirmed.
            const digestNode = (data as any)?.loadDataDigest;
            if (typeof digestNode?.scopeLabel === 'string') {
              setScopeLabel(digestNode.scopeLabel);
            }

            const updateNode =
              (data as any)?.directAnswer ||
              (data as any)?.generateResponse ||
              (data as any)?.dataInsight;
            if (updateNode?.messages && Array.isArray(updateNode.messages)) {
              const aiMsg = [...updateNode.messages].reverse().find((m: any) => m?.type === 'ai');
              if (aiMsg?.content && typeof aiMsg.content === 'string' && !aiMsg.content.startsWith('{')) {
                setMessages((prev) => {
                  const newArr = [...prev];
                  if (
                    newArr.length > 0 &&
                    newArr[newArr.length - 1].role === 'assistant'
                  ) {
                    newArr[newArr.length - 1].content = aiMsg.content;
                    newArr[newArr.length - 1].sources =
                      lastRetrievedDocsRef.current;
                  }
                  return newArr;
                });
              }
            }
          }
        }
      }
    } catch (error) {
      console.error('Error sending message:', error);
      toast({
        title: 'Lỗi gửi tin nhắn',
        description:
          error instanceof Error ? error.message : 'Đã có lỗi xảy ra khi xử lý phản hồi.',
        variant: 'destructive',
      });
      setMessages((prev) => {
        const newArr = [...prev];
        if (newArr.length > 0 && newArr[newArr.length - 1].role === 'assistant') {
          newArr[newArr.length - 1].content =
            'Rất tiếc, đã xảy ra lỗi trong quá trình xử lý câu hỏi. Vui lòng thử lại!';
        }
        return newArr;
      });
    } finally {
      setIsLoading(false);
      abortControllerRef.current = null;
    }
  };

  const handleFileUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const selectedFiles = Array.from(e.target.files || []);
    if (selectedFiles.length === 0) return;

    const nonPdfFiles = selectedFiles.filter(
      (file) => file.type !== 'application/pdf',
    );
    if (nonPdfFiles.length > 0) {
      toast({
        title: 'Định dạng file không hợp lệ',
        description: 'Vui lòng chỉ tải lên file định dạng .pdf',
        variant: 'destructive',
      });
      return;
    }

    // No ticket means this frame was not opened from the HRM portal (someone typed the
    // chatbot address directly, or the ten-minute ticket expired). The ingest route would
    // refuse with 403, which reads as "you lack permission" when the truth is "you are not
    // signed in here". Say which one it is, and skip a round trip that cannot succeed.
    if (!accessTokenRef.current) {
      toast({
        title: 'Chưa có phiên làm việc',
        description:
          'Hãy mở Trợ lý AI từ nút nổi trong cổng Quản trị (hoặc trang /AiAssistant) để nạp tài liệu. Mở trực tiếp địa chỉ chatbot không kèm quyền nạp.',
        variant: 'destructive',
      });
      if (fileInputRef.current) {
        fileInputRef.current.value = '';
      }
      return;
    }

    setIsUploading(true);
    try {
      const formData = new FormData();
      selectedFiles.forEach((file) => {
        formData.append('files', file);
      });
      // The upload is authorised by the same short-lived ticket the chat uses. Without it the
      // route refuses: the knowledge base is shared state, so writing to it needs a proven
      // identity and a permission the server checks, not a button that happens to be visible.
      // Presence was already checked above, so this is always set here.
      formData.append('ticket', accessTokenRef.current);

      const response = await fetch('/api/ingest', {
        method: 'POST',
        body: formData,
      });

      if (!response.ok) {
        const data = await response.json();
        throw new Error(data.error || 'Failed to upload files');
      }

      setFiles((prev) => [...prev, ...selectedFiles]);
      toast({
        title: 'Tải tài liệu thành công!',
        description: `Đã nạp ${selectedFiles.length} tài liệu PDF vào vector store. Bây giờ bạn có thể đặt câu hỏi về tài liệu này.`,
      });
    } catch (error) {
      console.error('Error uploading files:', error);
      toast({
        title: 'Tải tài liệu thất bại',
        description:
          error instanceof Error ? error.message : 'Không thể xử lý file PDF.',
        variant: 'destructive',
      });
    } finally {
      setIsUploading(false);
      if (fileInputRef.current) {
        fileInputRef.current.value = '';
      }
    }
  };

  const handleRemoveFile = (fileToRemove: File) => {
    setFiles(files.filter((file) => file !== fileToRemove));
    toast({
      title: 'Đã gỡ tài liệu',
      description: `Đã xóa ${fileToRemove.name}`,
    });
  };

  return (
    <div className="flex flex-col min-h-screen bg-slate-50 text-slate-900">
      {/* Top Navigation Header - Luxury 5★ Deep Navy & Gold (ẩn khi embed vào iframe drawer để tránh lặp 2 header) */}
      {!isEmbedded ? (
        <header className="sticky top-0 z-50 flex items-center justify-between px-4 sm:px-6 py-3 bg-gradient-to-r from-[#0f172a] via-[#1e3a8a] to-[#0f172a] text-white border-b border-[#d4af37]/40 shadow-md">
          <div className="flex items-center gap-3">
            <div className="relative flex items-center justify-center w-10 h-10 rounded-xl bg-white/10 backdrop-blur-sm border border-[#d4af37]/50 p-1 shadow-inner">
              <img
                src="/hotel-logo.png"
                alt="The OverTime Hotels"
                className="w-full h-full object-contain"
                onError={(e) => {
                  // Fallback nếu ảnh không nạp được
                  e.currentTarget.style.display = 'none';
                }}
              />
              <Sparkles className="w-5 h-5 text-[#fde047] absolute" style={{ display: 'none' }} />
            </div>
            <div>
              <div className="flex items-center gap-2">
                <h1 className="text-sm sm:text-base font-bold text-white tracking-wide">
                  The OverTime Hotel
                </h1>
                <span className="inline-flex items-center gap-1.5 px-2.5 py-0.5 text-[10.5px] font-semibold rounded-full bg-amber-400/20 text-[#fde047] border border-[#d4af37]/50 shadow-2xs">
                  <span className="w-1.5 h-1.5 rounded-full bg-emerald-400 animate-pulse" />
                  5★ Concierge HR AI
                </span>
              </div>
              <p className="text-[11px] text-slate-300">
                Trợ lý Quản trị Nhân sự &amp; Phân tích Văn bản Khách sạn
              </p>
            </div>
          </div>

          <div className="flex items-center gap-2">
            {messages.length > 0 && (
              <Button
                variant="outline"
                size="sm"
                onClick={handleResetChat}
                className="text-xs h-8 bg-white/10 hover:bg-[#d4af37]/20 text-[#fde047] hover:text-white border-[#d4af37]/50 backdrop-blur-xs rounded-xl transition-all"
              >
                <RotateCcw className="w-3.5 h-3.5 mr-1" />
                Làm mới chat
              </Button>
            )}
          </div>
        </header>
      ) : messages.length > 0 ? (
        <div className="sticky top-0 z-30 flex items-center justify-between px-3 py-1.5 bg-slate-100/90 backdrop-blur-xs border-b border-slate-200 text-xs">
          <span className="flex items-center gap-1.5 font-semibold text-slate-700 text-[11px]">
            <Sparkles className="w-3.5 h-3.5 text-[#d4af37]" />
            <span>Trợ lý AI 5★ · The OverTime Hotel</span>
            {scopeLabel && (
              <span
                className="ml-1 inline-flex items-center gap-1 rounded-full border border-[#d4af37]/50 bg-amber-50 px-2 py-0.5 font-medium text-[10px] text-amber-800"
                title="Phạm vi số liệu Trợ lý AI được phép đọc theo vai trò của bạn"
              >
                Phạm vi: {scopeLabel}
              </span>
            )}
          </span>
          <button
            type="button"
            onClick={handleResetChat}
            className="inline-flex items-center gap-1 text-[11px] font-medium text-slate-600 hover:text-slate-900 bg-white px-2 py-0.5 rounded-md border border-slate-200 transition-colors shadow-2xs"
          >
            <RotateCcw className="w-3 h-3 text-[#d4af37]" />
            Làm mới hội thoại
          </button>
        </div>
      ) : null}

      {/* Main Content Area */}
      <main className="flex-1 flex flex-col items-center justify-start w-full max-w-4xl mx-auto px-2.5 sm:px-4 py-2 sm:py-4 pb-28">
        {messages.length === 0 ? (
          <div className="w-full my-auto py-4 sm:py-6 flex flex-col items-center">
            {/* Hero Card - Luxury Hotel Welcome */}
            <div className="w-full max-w-2xl bg-white border border-[#d4af37]/30 border-t-4 border-t-[#d4af37] rounded-3xl p-5 sm:p-7 shadow-sm text-center mb-5">
              <div className="inline-flex p-3 rounded-2xl bg-gradient-to-br from-[#0f172a] to-[#1e3a8a] text-[#fde047] border border-[#d4af37]/50 shadow-md mb-2.5">
                <Sparkles className="w-6 h-6 sm:w-7 sm:h-7" />
              </div>
              <h2 className="text-lg sm:text-xl md:text-2xl font-bold text-slate-900 mb-1.5">
                Trợ lý AI Khách sạn The OverTime Hotel
              </h2>
              <p className="text-xs sm:text-sm text-slate-600 max-w-lg mx-auto mb-5 leading-relaxed">
                Hỏi đáp thông minh về quy chế khách sạn 5★, phân ca 24/7, chính sách tiền lương 2 tầng, phụ cấp ca đêm (+30%) và trích xuất dữ liệu từ các tệp PDF văn bản nội bộ.
              </p>

              {/* Upload trigger button */}
              <div
                onClick={() => fileInputRef.current?.click()}
                className="group border-2 border-dashed border-[#d4af37]/40 hover:border-[#d4af37] bg-gradient-to-b from-amber-50/30 to-amber-50/10 hover:from-amber-50/50 hover:to-amber-50/30 rounded-2xl p-4 sm:p-5 cursor-pointer transition-all flex flex-col items-center justify-center max-w-md mx-auto shadow-2xs"
              >
                <div className="p-2.5 rounded-full bg-white shadow-sm text-amber-700 group-hover:text-[#1e3a8a] mb-1.5 transition-colors border border-amber-200">
                  <UploadCloud className="w-5 h-5 sm:w-6 sm:h-6 text-[#d4af37]" />
                </div>
                <p className="text-xs sm:text-sm font-semibold text-slate-800 group-hover:text-[#1e3a8a]">
                  {isUploading ? 'Đang nạp dữ liệu PDF vào vector store...' : 'Bấm vào đây để tải lên tệp PDF'}
                </p>
                <p className="text-[11px] text-slate-400 mt-1">
                  Hỗ trợ tải lên Hợp đồng lao động, Quy chế, Sổ tay nhân viên (Tối đa 5 file, &lt; 10MB)
                </p>
                <p className="text-[10px] text-slate-400 mt-1">
                  Cần phiên đăng nhập Web Admin và quyền nạp tài liệu (Nhân sự / Quản trị viên)
                </p>
              </div>
            </div>

            {/* Suggested Prompts */}
            <div className="w-full max-w-2xl">
              <p className="text-xs font-bold uppercase tracking-wider text-[#b45309] mb-2.5 text-center flex items-center justify-center gap-1.5">
                <Sparkles className="w-3.5 h-3.5 text-[#d4af37]" />
                <span>Gợi ý câu hỏi bắt đầu chuẩn 5★</span>
              </p>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
                {SUGGESTED_PROMPTS.map((prompt, i) => (
                  <button
                    key={i}
                    onClick={() => setInput(prompt)}
                    className="p-3 text-left text-xs sm:text-[13px] font-medium text-slate-700 bg-white hover:bg-amber-50/30 border border-slate-200/90 hover:border-[#d4af37] rounded-xl shadow-2xs hover:shadow-xs transition-all flex items-center justify-between group"
                  >
                    <span className="leading-snug">{prompt}</span>
                    <ArrowUp className="w-3.5 h-3.5 text-slate-300 group-hover:text-[#d4af37] rotate-45 transition-colors shrink-0 ml-1.5" />
                  </button>
                ))}
              </div>
            </div>
          </div>
        ) : (
          <div className="w-full space-y-2.5 my-1">
            {messages.map((message, i) => (
              <ChatMessage key={i} message={message} />
            ))}
            <div ref={messagesEndRef} />
          </div>
        )}
      </main>

      {/* Floating Bottom Input Bar - 5★ Luxury Glassmorphism */}
      <div className="fixed bottom-0 left-0 right-0 z-40 bg-gradient-to-t from-slate-100 via-slate-100/95 to-transparent pt-2 pb-3 px-2.5 sm:px-4">
        <div className="w-full max-w-4xl mx-auto space-y-1.5">
          {/* Active Uploaded Files List */}
          {files.length > 0 && (
            <div className="flex flex-wrap gap-2 max-h-24 overflow-y-auto p-1">
              {files.map((file, index) => (
                <FilePreview
                  key={`${file.name}-${index}`}
                  file={file}
                  onRemove={() => handleRemoveFile(file)}
                />
              ))}
            </div>
          )}

          {/* Chat Form Container */}
          <form
            onSubmit={handleSubmit}
            className="flex items-center gap-2 bg-white rounded-2xl border border-slate-200/90 shadow-lg shadow-slate-900/5 p-1.5 focus-within:border-[#d4af37] focus-within:ring-2 focus-within:ring-[#d4af37]/20 transition-all"
          >
            <input
              type="file"
              ref={fileInputRef}
              onChange={handleFileUpload}
              accept=".pdf"
              multiple
              className="hidden"
            />

            <Button
              type="button"
              variant="ghost"
              size="icon"
              className="rounded-xl h-10 w-10 text-slate-500 hover:text-[#d4af37] hover:bg-amber-50/50 shrink-0 transition-colors"
              onClick={() => fileInputRef.current?.click()}
              disabled={isUploading}
              title="Đính kèm tệp PDF để tra cứu"
            >
              {isUploading ? (
                <Loader2 className="h-4 w-4 animate-spin text-[#d4af37]" />
              ) : (
                <Paperclip className="h-4 w-4" />
              )}
            </Button>

            <Input
              value={input}
              onChange={(e) => setInput(e.target.value)}
              placeholder={
                isUploading
                  ? 'Đang nạp và đối soát dữ liệu PDF...'
                  : !threadId
                  ? 'Đang khởi tạo kết nối backend...'
                  : 'Đặt câu hỏi về quy chế, ca trực, tiền lương hoặc PDF...'
              }
              className="border-0 shadow-none focus-visible:ring-0 focus-visible:ring-offset-0 text-sm h-10 bg-transparent px-2 placeholder:text-slate-400"
              disabled={isUploading || isLoading || !threadId}
            />

            <Button
              type="submit"
              size="icon"
              className="rounded-xl h-10 w-10 bg-gradient-to-r from-[#0f172a] via-[#1e3a8a] to-[#0f172a] hover:from-[#1e3a8a] hover:to-[#0f172a] text-[#fde047] border border-[#d4af37]/50 shrink-0 shadow-md transition-all disabled:opacity-40 disabled:hover:scale-100"
              disabled={!input.trim() || isUploading || isLoading || !threadId}
              title="Gửi câu hỏi"
            >
              {isLoading ? (
                <Loader2 className="h-4 w-4 animate-spin text-[#fde047]" />
              ) : (
                <ArrowUp className="h-4 w-4 text-[#fde047]" />
              )}
            </Button>
          </form>

          <p className="text-[11px] text-center text-slate-400 font-normal">
            Trợ lý AI Khách sạn 5★ The OverTime Hotel · Đối soát quy chế nội bộ &amp; tài liệu PDF được nạp.
          </p>
        </div>
      </div>
    </div>
  );
}
