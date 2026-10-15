import { FileText, X } from "lucide-react"
import { Button } from "@/components/ui/button"

interface FilePreviewProps {
  file: File
  onRemove: () => void
}

export function FilePreview({ file, onRemove }: FilePreviewProps) {
  return (
    <div className="flex items-center gap-2.5 bg-white border border-[#d4af37]/40 rounded-xl px-3 py-1.5 shadow-2xs">
      <div className="w-7 h-7 bg-gradient-to-br from-[#1e3a8a] to-[#0f172a] rounded-lg flex items-center justify-center text-[#fde047] border border-[#d4af37]/50">
        <FileText className="w-3.5 h-3.5" />
      </div>
      <div className="flex-1 min-w-0 max-w-[180px]">
        <p className="text-xs font-semibold truncate text-slate-800">{file.name}</p>
        <p className="text-[10px] text-amber-700 font-medium">PDF Đã nạp</p>
      </div>
      <Button
        variant="ghost"
        size="icon"
        className="h-6 w-6 text-slate-400 hover:text-rose-600 hover:bg-rose-50 rounded-md transition-colors"
        onClick={onRemove}
        title="Gỡ file"
      >
        <X className="h-3.5 w-3.5" />
      </Button>
    </div>
  )
}

