import { Copy, Check, Sparkles, User, FileText } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { useState } from 'react';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import { PDFDocument } from '@/types/graphTypes';
import {
  Accordion,
  AccordionContent,
  AccordionItem,
  AccordionTrigger,
} from '@/components/ui/accordion';

interface ChatMessageProps {
  message: {
    role: 'user' | 'assistant';
    content: string;
    sources?: PDFDocument[];
  };
}

export function ChatMessage({ message }: ChatMessageProps) {
  const isUser = message.role === 'user';
  const [copied, setCopied] = useState(false);
  const isLoading = message.role === 'assistant' && message.content === '';

  const handleCopy = async () => {
    try {
      await navigator.clipboard.writeText(message.content);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch (err) {
      console.error('Failed to copy text:', err);
    }
  };

  const showSources =
    message.role === 'assistant' &&
    message.sources &&
    message.sources.length > 0;

  return (
    <div className={`flex gap-2 sm:gap-3 my-2.5 w-full ${isUser ? 'justify-end' : 'justify-start'}`}>
      {!isUser && (
        <div className="relative flex items-center justify-center w-8 h-8 sm:w-9 sm:h-9 rounded-full bg-gradient-to-br from-[#0f172a] via-[#1e3a8a] to-[#0f172a] text-[#fde047] shrink-0 mt-0.5 shadow-md border border-[#d4af37]/60 ring-2 ring-[#d4af37]/20">
          <Sparkles className="w-3.5 h-3.5 sm:w-4 sm:h-4 text-[#fde047]" />
          <span className="absolute -bottom-1 -right-1 flex items-center justify-center w-3.5 h-3.5 sm:w-4 sm:h-4 rounded-full bg-[#d4af37] text-[#0f172a] font-bold text-[8.5px] sm:text-[9px] shadow-xs">
            ★
          </span>
        </div>
      )}

      <div
        className={`rounded-2xl px-3.5 py-2.5 sm:px-4.5 sm:py-3.5 shadow-sm transition-all ${
          isUser
            ? 'max-w-[90%] sm:max-w-[85%] bg-gradient-to-r from-[#1e3a8a] to-[#0f172a] text-white rounded-tr-xs border border-[#d4af37]/30 shadow-blue-900/10'
            : 'flex-1 min-w-0 max-w-[96%] sm:max-w-[94%] bg-white border border-slate-200/90 hover:border-[#d4af37]/40 text-slate-800 rounded-tl-xs shadow-slate-200/50'
        }`}
      >
        {isLoading ? (
          <div className="flex items-center gap-2 py-1 px-1">
            <span className="text-xs text-slate-400 font-medium italic">Đang suy nghĩ &amp; tra cứu...</span>
            <div className="flex space-x-1.5 items-center">
              <div className="w-2 h-2 bg-[#d4af37] rounded-full animate-bounce [animation-delay:-0.3s]" />
              <div className="w-2 h-2 bg-[#d4af37] rounded-full animate-bounce [animation-delay:-0.15s]" />
              <div className="w-2 h-2 bg-[#d4af37] rounded-full animate-bounce" />
            </div>
          </div>
        ) : (
          <>
            {!isUser && (
              <div className="flex items-center justify-between gap-2 mb-2 pb-1.5 border-b border-slate-100">
                <div className="flex items-center gap-1.5">
                  <span className="text-[12px] font-bold text-[#0f172a] tracking-tight">
                    Trợ lý Khách sạn 5★
                  </span>
                  <span className="px-1.5 py-0.2 rounded-full text-[10px] font-semibold bg-amber-50 text-amber-700 border border-amber-200">
                    The OverTime Hotel
                  </span>
                </div>
                <Button
                  variant="ghost"
                  size="sm"
                  className="h-6 px-2 text-[11px] text-slate-400 hover:text-slate-800 hover:bg-slate-100 rounded-md transition-colors"
                  onClick={handleCopy}
                  title="Sao chép câu trả lời"
                >
                  {copied ? (
                    <>
                      <Check className="h-3 w-3 mr-1 text-emerald-600" />
                      <span className="text-emerald-600 font-medium">Đã chép</span>
                    </>
                  ) : (
                    <>
                      <Copy className="h-3 w-3 mr-1" />
                      <span>Sao chép</span>
                    </>
                  )}
                </Button>
              </div>
            )}

            {isUser ? (
              <div className="whitespace-pre-wrap leading-relaxed text-[13.5px] sm:text-[14.5px] text-white">
                {message.content}
              </div>
            ) : (
              <div className="prose prose-sm prose-slate max-w-none text-[13.5px] sm:text-[14.5px] leading-relaxed text-slate-700 prose-headings:text-slate-900 prose-headings:font-bold prose-headings:text-base prose-p:my-2 prose-ul:my-2 prose-ol:my-2 prose-li:my-0.5 prose-strong:text-slate-900 prose-strong:font-bold">
                <ReactMarkdown remarkPlugins={[remarkGfm]}>
                  {message.content}
                </ReactMarkdown>
              </div>
            )}

            {showSources && message.sources && (
              <Accordion type="single" collapsible className="w-full mt-3 border-t border-slate-100">
                <AccordionItem value="sources" className="border-0">
                  <AccordionTrigger className="text-[11.5px] text-[#1e3a8a] py-2 justify-start gap-1.5 hover:no-underline font-semibold">
                    <FileText className="w-3.5 h-3.5 text-[#d4af37]" />
                    <span>Nguồn trích xuất từ tài liệu PDF ({message.sources.length} đoạn)</span>
                  </AccordionTrigger>
                  <AccordionContent>
                    <div className="grid grid-cols-1 sm:grid-cols-2 gap-2 pt-1 pb-1">
                      {message.sources?.map((source, index) => (
                        <Card
                          key={index}
                          className="bg-slate-50/70 border border-slate-200/80 hover:border-[#d4af37]/60 transition-all rounded-xl shadow-2xs"
                        >
                          <CardContent className="p-2.5">
                            <div className="flex items-center gap-1.5 mb-1">
                              <span className="px-1.5 py-0.5 rounded text-[9.5px] font-bold bg-[#1e3a8a] text-white">
                                PDF
                              </span>
                              <p className="text-[11.5px] font-semibold text-slate-800 truncate flex-1">
                                {source.metadata?.source ||
                                  source.metadata?.filename ||
                                  'Tài liệu khách sạn'}
                              </p>
                            </div>
                            <p className="text-[10.5px] text-slate-500 flex items-center justify-between">
                              <span>Trang: <strong className="text-slate-700">{source.metadata?.loc?.pageNumber || 'N/A'}</strong></span>
                              <span className="text-[9.5px] text-[#b45309] font-medium">Đã đối soát vector</span>
                            </p>
                          </CardContent>
                        </Card>
                      ))}
                    </div>
                  </AccordionContent>
                </AccordionItem>
              </Accordion>
            )}
          </>
        )}
      </div>

      {isUser && (
        <div className="flex items-center justify-center w-9 h-9 rounded-full bg-gradient-to-tr from-slate-700 to-slate-900 text-slate-200 shrink-0 mt-0.5 shadow-sm border border-slate-600">
          <User className="w-4 h-4" />
        </div>
      )}
    </div>
  );
}
