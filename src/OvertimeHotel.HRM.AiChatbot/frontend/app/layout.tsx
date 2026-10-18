import type React from "react"
import type { Metadata } from "next"
import { GeistSans } from "geist/font/sans"
import { Toaster } from "@/components/ui/toaster"

import "katex/dist/katex.min.css"
import "./globals.css"

export const metadata: Metadata = {
  title: "The OverTime Hotel · Trợ lý AI Khách sạn 5★",
  description: "Hệ thống Quản trị Nhân sự & Phân tích Văn bản Khách sạn The OverTime Hotel",
}

export default function RootLayout({
  children,
}: {
  children: React.ReactNode
}) {
  return (
    <html lang="en">
      <body className={GeistSans.className}>
        {children}
        <Toaster />
      </body>
    </html>
  )
}