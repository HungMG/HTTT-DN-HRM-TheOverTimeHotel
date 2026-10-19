# PRD 07: Trợ Lý AI Khách Sạn — Cấu Trúc Phân Hệ & Logic Xử Lý

> **Trạng thái tài liệu:** mô tả **hiện trạng đã triển khai trong mã nguồn** tại tháng 10/2026.
> Mọi khẳng định về hành vi bên dưới đối chiếu được với file thật trong `src/OvertimeHotel.HRM.AiChatbot/`
> và các file tích hợp phía Web Admin. Phần nào **chưa triển khai** được đánh dấu rõ là *khoảng trống*
> (xem mục 12) thay vì mô tả như đã có.
>
> **Bản cập nhật 19/10/2026:** bổ sung §12.1 — phân quyền kho tri thức (vé bắt buộc khi nạp, phạm vi
> tài liệu theo phòng ban, quyền `QL_TRI_THUC_AI`). Bốn khoảng trống #1–#4 nay đã đóng.

---

## 1. Vị Trí Phân Hệ Trong Hệ Thống

Trợ lý AI **không** phải một màn hình của Web Admin. Nó là một **tiến trình độc lập** (Next.js + LangGraph),
được Web Admin **nhúng bằng `iframe`** và được cấp một **vé dữ liệu ngắn hạn** để hỏi ngược số liệu.

Lý do kiến trúc: mô hình ngôn ngữ phải chạy bằng Node/TypeScript (hệ sinh thái LangChain/LangGraph),
trong khi nghiệp vụ HRM nằm ở C# .NET 10. Hai bên giao tiếp qua **một cổng HTTP duy nhất** thay vì
dùng chung CSDL.

```text
┌───────────────────────────────────────────────────────────────────────────────┐
│                          BA TIẾN TRÌNH ĐỘC LẬP                                │
└───────────────────────────────────────────────────────────────────────────────┘

  [Quản trị viên / HR / Quản lý]          ← đã đăng nhập cookie phiên
              │
              ▼
┌──────────────────────────────────────┐
│  Web Admin · ASP.NET Core MVC (.NET) │   http://localhost:5065
│  AiAssistantController               │
│  AiChatbotUrlBuilder                 │   dựng URL nhúng + ký vé
│  AiContextTokenService               │   JWT HS256, TTL 10 phút
└───────┬──────────────────────────────┘
        │  <iframe src="{chatbot}?embed=true&page=…&tab=…
        │                       &nam=2026&thang=10#bt={vé}">
        ▼
┌──────────────────────────────────────┐
│  Frontend · Next.js 14 App Router    │   http://localhost:3000
│  app/page.tsx · app/api/chat         │   Node runtime, proxy SSE
│  app/api/ingest                      │   Node runtime, multipart
│  lib/knowledge-access.ts             │   hỏi quyền đọc/nạp kho tri thức
└───┬──────────────┬───────────────────┘
    │              │  WEBADMIN_API_URL
    │              │  POST /api/ai-data/knowledge-access { token }
    │              ▼
    │      ┌──────────────────────────────────────────┐
    │      │ Web Admin · KnowledgeAccessPolicy        │
    │      │ → documentScopes / ingestScopes / canIngest│
    │      └──────────────────────────────────────────┘
    │  LangGraph SDK (HTTP + SSE)
    ▼
┌──────────────────────────────────────┐
│  Backend · LangGraph JS (Node 20)    │   http://localhost:2024
│  retrieval_graph · ingestion_graph   │
└───────┬───────────────────┬──────────┘
        │                   │
        │ (a) RAG           │ (b) DATA_API_URL — POST /api/ai-data/digest
        ▼                   ▼
┌──────────────────┐  ┌──────────────────────────────────┐
│ Supabase         │  │ Web Admin back-channel           │
│ pgvector         │  │ (đúng vé → truy vấn CSDL → JSON) │
│ documents        │  └──────────────────────────────────┘
│ metadata.scope   │
└──────────────────┘
```

**Bốn đường giao tiếp và ranh giới bảo mật của từng đường:**

| Đường | Ai gọi ai | Xác thực | Ranh giới |
| :--- | :--- | :--- | :--- |
| Trình duyệt → Next.js | `POST /api/chat`, `POST /api/ingest` | Vé gửi trong thân yêu cầu | Vé nằm ở fragment `#bt=`, trình duyệt **không tự gửi lên máy chủ**; chỉ `page.tsx` đọc ra rồi chuyển tiếp |
| Next.js → Web Admin | `POST /api/ai-data/knowledge-access` | Vé trong thân JSON | Next.js **không tự phán quyền**: hỏi Web Admin, nhận về danh sách scope rồi ghi đè `configurable` |
| Next.js → LangGraph | LangGraph SDK `runs.stream` | `LANGCHAIN_API_KEY` (tuỳ chọn) | `configurable` do **trình duyệt** cung cấp — coi là dữ liệu không tin cậy; `docScopes` được ghi đè sau cùng |
| LangGraph → Web Admin | `POST /api/ai-data/digest` | Vé JWT HS256, audience riêng | URL **chỉ** đọc từ `process.env.DATA_API_URL`, không bao giờ từ `configurable` |

---

## 2. Cấu Trúc Thư Mục Thực Tế

```text
src/OvertimeHotel.HRM.AiChatbot/
├── backend/                              # LangGraph server (Node 20, ESM)
│   ├── langgraph.json                    # Đăng ký 2 graph + tên biến môi trường
│   ├── package.json                      # @langchain/langgraph, @supabase/supabase-js
│   ├── .env / .env.example               # AI_API_KEY, DATA_API_URL, SUPABASE_*
│   ├── demo.ts                           # Script chạy thử graph ngoài server
│   ├── ingest-demo.ipynb                 # Notebook nạp tài liệu mẫu
│   ├── test_docs/                        # PDF + docSplits.json phục vụ kiểm thử
│   ├── __tests__/
│   │   ├── ingestion_graph/state.test.ts
│   │   └── retrieval_graph/
│   │       ├── promptTemplate.test.ts    # Kiểm tra guardrail/ký hiệu LaTeX trong prompt
│   │       └── integration.test.ts       # Gọi thật Supabase (bỏ qua nếu thiếu env)
│   └── src/
│       ├── shared/                       # Dùng chung cho cả 2 graph
│       │   ├── configuration.ts          # retrieverProvider, filterKwargs, k, docScopes
│       │   ├── retrieval.ts              # makeRetriever (theo phạm vi) / makeIndexingRetriever (đường ghi)
│       │   ├── state.ts                  # reduceDocs
│       │   └── utils.ts                  # loadChatModel, SUPPORTED_PROVIDERS
│       ├── ingestion_graph/
│       │   ├── graph.ts                  # START → ingestDocs → END
│       │   ├── state.ts                  # docs
│       │   └── configuration.ts          # docsFile, useSampleDocs
│       ├── retrieval_graph/
│       │   ├── graph.ts                  # ★ Toàn bộ logic định tuyến & trả lời
│       │   ├── state.ts                  # query, route, documents, dataDigest…
│       │   ├── configuration.ts          # accessToken, pageContext, year/month/quarter
│       │   ├── prompts.ts                # 4 prompt hệ thống
│       │   └── utils.ts                  # formatDocs → XML
│       └── sample_docs.json              # Tài liệu mẫu (chỉ khi useSampleDocs=true)
│
└── frontend/                             # Next.js 14 (App Router)
    ├── app/
    │   ├── page.tsx                      # ★ Toàn bộ khung chat + đọc vé từ fragment
    │   ├── layout.tsx                    # Theme, font
    │   ├── globals.css
    │   └── api/
    │       ├── chat/route.ts             # ★ Proxy SSE, runtime = 'nodejs'; ghi đè docScopes
    │       └── ingest/route.ts           # ★ Đòi vé + quyền → Upload PDF → ingestion graph
    ├── components/
    │   ├── chat-message.tsx              # Markdown + KaTeX + accordion nguồn
    │   ├── file-preview.tsx
    │   ├── example-prompts.tsx
    │   ├── theme-provider.tsx
    │   └── ui/                           # 49 component shadcn/ui
    ├── constants/graphConfigs.ts         # queryModel, k, retrieverProvider
    ├── lib/
    │   ├── langgraph-client.ts           # SDK phía trình duyệt
    │   ├── langgraph-server.ts           # SDK phía máy chủ (singleton)
    │   ├── langgraph-base.ts             # Bọc threads/runs
    │   ├── knowledge-access.ts           # ★ Hỏi Web Admin quyền đọc/nạp; fail-closed
    │   └── pdf.ts                        # PDFLoader → Document[]
    ├── types/graphTypes.ts               # Kiểu state/config chia sẻ
    └── __tests__/api/ingest/             # Kiểm thử route upload
```

---

## 3. Điểm Nhúng Phía Web Admin

Khung chat xuất hiện ở **hai nơi**, và cả hai **dùng chung một bộ dựng URL**
(`AiChatbotUrlBuilder`) để không lệch nhau:

| Điểm nhúng | Tệp | Hành vi |
| :--- | :--- | :--- |
| Drawer nổi (mọi trang) | `Views/Shared/_Layout.cshtml` | `offcanvas` bên phải, rộng `580px` ↔ `820px`, có nút làm mới và mở tab riêng |
| Trang riêng | `Views/AiAssistant/Index.cshtml` (`[Authorize]`) | Banner 5★ + bento hướng dẫn + `iframe` toàn chiều rộng |

**Cấu trúc URL nhúng do máy chủ dựng:**

```text
{chatbotUrl}?embed=true
             &page={Controller}
             &action={Action}
             &tab={tab}
             &period={nhãn đọc được}
             &nam={2000..2100}
             &thang={1..12}
             &quy={1..4}
             #bt={vé JWT}
```

**Vì sao có cả `period` (chữ) lẫn `nam/thang/quy` (số):** `period` để mô hình biết người dùng đang xem
kỳ nào; ba tham số số để backend **dựng lại đúng kỳ đó** khi gọi cầu dữ liệu. Chỉ có nhãn chữ thì không
thể quay ngược thành truy vấn, và mọi câu hỏi sẽ âm thầm rơi về "cả năm".

**Vì sao vé nằm ở fragment (`#bt=`) chứ không phải query string:** fragment **không được trình duyệt gửi
lên máy chủ** Next.js, nên vé không lọt vào access log và không bị mang theo qua header `Referer`.
`page.tsx` đọc vé **một lần** lúc mount rồi `history.replaceState` xoá nó khỏi thanh địa chỉ và lịch sử.

---

## 4. Frontend: Luồng Trang Chat

### 4.1. Trạng thái cục bộ (`app/page.tsx`)

| State / Ref | Kiểu | Vai trò |
| :--- | :--- | :--- |
| `messages` | `{role, content, sources?}[]` | Lịch sử hiển thị; luôn đẩy **hai** bản ghi (user + assistant rỗng) trước khi stream |
| `threadId` | `string \| null` | LangGraph thread; tạo lúc mount, làm mới khi bấm "Làm mới chat" |
| `isLoading` / `isUploading` | `boolean` | Khoá nút gửi và nút đính kèm |
| `isEmbedded` | `boolean` | `window.self !== window.top` hoặc có `?embed` → ẩn header lớn, dùng header mỏng |
| `scopeLabel` | `string` | Phạm vi dữ liệu máy chủ trả về; hiện thành badge vàng trong header mỏng |
| `accessTokenRef` | `ref` | Vé, đọc một lần lúc mount — **cố ý không đưa vào state** để không đọc lại địa chỉ |
| `pageContextRef` | `ref` | Ghép từ `page/action/tab/period` |
| `periodRef` | `ref` | `{year?, month?, quarter?}` dạng số |
| `lastRetrievedDocsRef` | `ref` | Tài liệu của lượt hiện tại, gắn vào `sources` của câu trả lời |

### 4.2. Vòng đời một câu hỏi

```text
Người dùng nhập → bấm Gửi
        │
        ▼
[1] đẩy {role:'user'} + {role:'assistant', content:''}  → hiện chấm "Đang suy nghĩ…"
        │
        ▼
[2] POST /api/chat  { message, threadId, accessToken, pageContext, year, month, quarter }
        │
        ▼
[3] Đọc SSE từng dòng "data: {...}"
        │
        ├── messages/metadata  → ghi nhớ id của message do
        │                        directAnswer | generateResponse | dataInsight sinh ra
        ├── messages/partial   → cập nhật nội dung đang chảy, CHỈ khi đúng id đó
        ├── messages/complete  → chốt nội dung cuối, cùng điều kiện id
        └── updates            → lấy documents (để hiện nguồn)
                                 lấy scopeLabel từ node loadDataDigest
                                 lấy messages từ node trả lời
        │
        ▼
[4] finally: isLoading = false
```

**Ba chi tiết chống lỗi đáng chú ý:**

1. **Bộ lọc theo `message id`, không theo thứ tự.** Khi bắt đầu stream, LangGraph phát lại lịch sử cũ;
   nếu không lọc theo id của lượt hiện tại thì tin nhắn cũ sẽ "nhảy" vào câu trả lời mới.
2. **Bỏ qua nội dung bắt đầu bằng `{`.** Dòng này chặn JSON của structured-output (router) lọt vào khung chat.
3. **`dataInsight` phải có trong danh sách node nhận.** Thiếu nó thì `activeAiMessageId` là `null`
   và mọi chunk của câu trả lời số liệu bị loại, khung chat đứng im.

### 4.3. Hiển thị (`components/chat-message.tsx`)

- `react-markdown` + `remark-gfm` + `remark-math` + `rehype-katex` → công thức lương hiển thị đúng.
- `message.content === ''` và role assistant → trạng thái "Đang suy nghĩ & tra cứu..." (ba chấm vàng kim),
  **không dùng spinner tròn** theo `rules/mobile-design-rules.md`.
- Nguồn tài liệu hiển thị trong `Accordion` (tên file, số trang).
- Nút copy nội dung, phản hồi 2 giây.

### 4.4. Upload tài liệu

| Kiểm tra | Giá trị |
| :--- | :--- |
| **Quyền** | Bắt buộc có vé; Web Admin phải trả `canIngest = true`, nếu không → **403** |
| Số file tối đa | 5 |
| Kích thước tối đa / file | 10 MB |
| Định dạng | Chỉ `application/pdf` |
| Phạm vi | `scope` chỉ nhận nếu nằm trong `ingestScopes` của vé; mặc định `all` |
| Xử lý | `processPDF` → `PDFLoader` → mỗi trang một `Document` + `metadata.filename` |
| Gắn nhãn | Mỗi chunk nhận `metadata.scope`, `.uploadedAt`, `.uploadedBy` |
| Đích | Tạo thread mới → chạy `LANGGRAPH_INGESTION_ASSISTANT_ID` (đọc **đồng bộ**, `runs.wait`) |
| Giới hạn | Route không ở Edge runtime (cần `fs`, `formData`) |

Vé đi kèm trong trường `ticket` của multipart body (không phải header, không phải URL — không lọt
access log). Giao diện chỉ hiện khung nạp khi phiên đăng nhập Web Admin còn hiệu lực.

---

## 5. Backend: `retrieval_graph` — Sơ Đồ Logic

Đây là **trái tim** của trợ lý. Toàn bộ nằm trong `backend/src/retrieval_graph/graph.ts`.

```text
                              ┌──────────┐
                              │  START   │
                              └────┬─────┘
                                   ▼
                        ┌────────────────────┐
                        │  checkQueryType    │  LLM + structured output
                        │  (router, zod)     │  → route ∈ {retrieve|direct|data|payslip}
                        └─────────┬──────────┘
                                  │
              ┌───────────────────┼────────────────────┐
              │ retrieve          │ direct             │ data | payslip
              ▼                   ▼                    ▼
   ┌──────────────────┐   ┌──────────────┐   ┌────────────────────┐
   │ retrieveDocuments│   │ directAnswer │   │  loadDataDigest    │
   │ RAG vector store │   │ DIRECT_SYS   │   │  POST WebAdmin     │
   └────────┬─────────┘   └──────┬───────┘   │  scope = reports | │
            │                    │           │          my-payslip │
            ▼                    │           └─────────┬──────────┘
   ┌──────────────────┐          │                     ▼
   │ generateResponse │          │           ┌────────────────────┐
   │ RESPONSE_SYS     │          │           │    dataInsight     │
   │ (context = docs) │          │           │    DATA_SYS        │
   └────────┬─────────┘          │           │ (context = digest) │
            │                    │           └─────────┬──────────┘
            └────────────────────┴──────────┬──────────┘
                                            ▼
                                         ┌─────┐
                                         │ END │
                                         └─────┘
```

### 5.1. Bảng node

| Node | Đầu vào từ state | Đầu ra ghi vào state | Mô hình / tài nguyên |
| :--- | :--- | :--- | :--- |
| `checkQueryType` | `query` | `route` | `queryModel` + `withStructuredOutput(zod)` |
| `retrieveDocuments` | `query`, `config.docScopes` | `documents` | `makeRetriever(config)` — một lượt tìm/scope, hợp nhất bằng RRF |
| `generateResponse` | `query`, `documents`, `messages` | `messages` (human + ai) | `RESPONSE_SYSTEM_PROMPT` |
| `directAnswer` | `query`, `messages` | `messages` | `DIRECT_SYSTEM_PROMPT` |
| `loadDataDigest` | `route`, config (`accessToken`, kỳ) | `dataDigest`, `dataError`, `scopeLabel` | `fetch` → `/api/ai-data/digest`, timeout 8 s |
| `dataInsight` | `query`, `dataDigest`, `dataError`, config | `messages` | `DATA_SYSTEM_PROMPT` |

### 5.2. Quy tắc định tuyến (router)

| Route | Khi nào | Dùng gì để trả lời |
| :--- | :--- | :--- |
| `retrieve` | Câu hỏi về **nội dung văn bản**: quy chế, hợp đồng, sổ tay, PDF đã nạp | Vector store → prompt có `{context}` |
| `data` | Câu hỏi về **số liệu sống**: quân số, biến động, chuyên cần, quỹ lương, hiệu quả sự kiện, hoặc "con số này nghĩa là gì" | Cầu dữ liệu, scope `reports` |
| `payslip` | Câu hỏi về **lương của chính người hỏi** | Cầu dữ liệu, scope `my-payslip` |
| `direct` | Chào hỏi, giới thiệu bản thân, ngoài phạm vi | Prompt trực tiếp, không tra cứu |

**Quy tắc "khi mơ hồ chọn `payslip`":** đây là *lát cắt hẹp hơn*, và cầu dữ liệu mới là nơi quyết định
trả gì — chọn nhầm sang `data` sẽ khiến người dùng nhận số liệu toàn khách sạn khi họ chỉ hỏi lương mình.

**Khi structured output thất bại:** router **fail-safe về `retrieve`** (tra tài liệu) và ghi cảnh báo,
không làm sập lượt chat.

### 5.3. Quy tắc "không bịa số"

Chuỗi xử lý được thiết kế để **im lặng không bao giờ bị lấp bằng số tưởng tượng**:

```text
loadDataDigest
   ├── thiếu DATA_API_URL hoặc vé rỗng → dataDigest='' + dataError = "thiếu cấu hình / hết phiên"
   ├── timeout 8 s (AbortSignal)       → dataDigest='' + dataError = "phản hồi quá chậm"
   ├── HTTP 403                        → dataDigest='' + dataError = "vai trò không được xem"
   ├── HTTP 401                        → dataDigest='' + dataError = "phiên đã hết hiệu lực, mở lại từ cổng"
   ├── HTTP khác                       → dataDigest='' + dataError = "hệ thống dữ liệu đang bận"
   └── mất kết nối                     → dataDigest='' + dataError = "chưa kết nối được"
                    │
                    ▼
dataInsight: context = dataDigest || "(không có số liệu) " + dataError
                    │
                    ▼
DATA_SYSTEM_PROMPT: "Nếu JSON rỗng … KHÔNG bịa số thay thế"
```

**Phía Web Admin cũng giữ nguyên tắc này:** CSDL không phản hồi → trả **503**, cố ý **không** trả số 0.
Trả 0 sẽ bị mô hình đọc thành "thực chi 0 đồng" — một câu trả lời sai nhưng nghe rất hợp lý.

**401 và 403 phải nói hai câu khác nhau.** Đây là hai ranh giới quyền khác hẳn nhau:

| Mã | Nghĩa thật | Người dùng cần làm gì |
| :--- | :--- | :--- |
| `403` | Vé còn tốt, nhưng **vai trò** không được xem số liệu | Xin cấp quyền, hoặc hỏi HR/Admin |
| `401` | **Vé đã hết hạn** (quá 10 phút); vai trò không liên quan | Mở lại Trợ lý AI từ cổng Quản trị |

Gộp cả hai vào một câu "hệ thống dữ liệu đang bận" là nói sai sự thật và đẩy người dùng đi nhầm hướng:
vé hết hạn thì chờ mãi cũng không xong, phải mở lại khung trợ lý. Vì vậy `loadDataDigest` tách riêng
nhánh `401` (xem `DataBridgeError`).

**Vì sao phải là `DataBridgeError` mang mã trạng thái:** so khớp chuỗi thông báo lỗi sẽ vỡ ngay khi
thư viện HTTP đổi câu chữ. Lớp lỗi riêng giữ nguyên mã trạng thái và cho phép phân nhánh ổn định.

### 5.4. State của graph

| Khoá | Kiểu | Ghi bởi | Ghi chú |
| :--- | :--- | :--- | :--- |
| `query` | `string` | Input của run | Câu hỏi thô của người dùng |
| `route` | `string` | `checkQueryType` | Một trong 4 giá trị ở trên |
| `messages` | `MessagesAnnotation` | 3 node trả lời | Bộ giảm mặc định của LangGraph (nối thêm) |
| `documents` | `Document[]` | `retrieveDocuments` | Giảm bằng `reduceDocs`, mặc định `[]` |
| `dataDigest` | `string` | `loadDataDigest` | JSON rút gọn; `''` = không có số liệu |
| `dataError` | `string` | `loadDataDigest` | Lý do **đọc được bằng tiếng Việt**, không lộ HTTP status |
| `scopeLabel` | `string` | `loadDataDigest` | Lấy từ chính digest, dùng cho badge trên header |

**Ba trường `dataDigest` / `dataError` / `scopeLabel` cố ý tách rời:** để node trả lời phân biệt
"không có quyền xem" với "hệ thống đang hỏng", và để khung chat hiện được phạm vi mà **máy chủ** xác nhận.

### 5.5. Configuration của graph

| Khoá | Mặc định | Nguồn | Có tin cậy? |
| :--- | :--- | :--- | :--- |
| `retrieverProvider` | `supabase` nếu có env, ngược lại `memory` | Trình duyệt / env | Có thể sai → chỉ ảnh hưởng nơi tra cứu |
| `filterKwargs` | `{}` | Trình duyệt | Bộ lọc metadata vector store |
| `k` | `5` | Trình duyệt | Số đoạn tài liệu lấy ra |
| `queryModel` | `openai/gpt-4o` | Trình duyệt | Bị `AI_MODEL` ghi đè nếu có |
| `accessToken` | `''` | Vé Web Admin | **Không tin cậy được**, chỉ chứng minh *ai*, không nói *được xem gì* |
| `pageContext` | `''` | Trình duyệt | Chỉ để diễn giải, **không mang quyền** |
| `year` / `month` / `quarter` | `0` | Trình duyệt | Chỉ **thu hẹp** kỳ, không mở rộng quyền |
| `docScopes` | `['all']` | **Máy chủ Next.js** | Phạm vi đọc tài liệu; trình duyệt gửi gì cũng bị ghi đè |

`toPositiveInteger` loại bỏ mọi giá trị không phải số nguyên dương; `0` được đọc là "không cung cấp"
để cầu dữ liệu dùng mặc định của nó thay vì nhận "năm 0 / tháng 0".

---

## 6. Cầu Dữ Liệu: Vé & `AiDataController`

### 6.1. Vì sao phải có

Chatbot là tiến trình Node **không có phiên đăng nhập** và **không được cầm khoá Supabase**.
Thay vì chia sẻ khoá, Web Admin ký một **vé ngắn hạn** chứa đúng danh tính người đang đăng nhập;
chatbot cầm vé gọi ngược về và **Web Admin tự truy vấn**.

### 6.2. Vòng đời vé

```text
[Web Admin] AiChatbotUrlBuilder.BuildEmbedUrl(User, …)
      │
      ├─ AiContextTokenService.Issue(User)
      │     • HS256, khoá riêng "Chatbot:AiTokenKey" (hoặc env AI_CONTEXT_TOKEN_KEY)
      │     • audience = "OverTimeHotel.AiData"   ← tách hẳn token mobile
      │     • hạn 10 phút, ClockSkew = 0
      │     • claim: sub, role, emp, dept, quyen.* (sao nguyên từ phiên)
      │     • ghi jti vào IMemoryCache 10 phút  → chỉ vé do máy chủ này phát mới nhận
      │
      ▼
   #bt={vé}  →  iframe  →  page.tsx đọc 1 lần  →  ref  →  POST /api/chat
      │
      ▼
[LangGraph] configurable.accessToken  →  POST /api/ai-data/digest { token, scope, year, month, quarter }
      │
      ▼
[Web Admin] AiContextTokenService.Validate(token)
      • sai chữ ký / hết hạn / sai audience / jti không có trong cache  →  null  →  401
      • đúng  →  dựng lại ClaimsPrincipal  →  ReportPolicy.ResolveScope(role, dept)
```

**Cùng một vé, hai vai trò.** Vé còn dùng cho hai việc khác hẳn nhau nên `AiDataController` có hai cửa:

| Cửa | Hỏi gì | Trả lời |
| :--- | :--- | :--- |
| `POST /api/ai-data/digest` | "Được xem **số liệu** nào?" | `AiDataDigestDto` — áp `ReportPolicy` |
| `POST /api/ai-data/knowledge-access` | "Được **đọc/nạp tài liệu** nào?" | `KnowledgeAccessDto` — áp `KnowledgeAccessPolicy` |

Cửa thứ hai cố ý **chỉ nhận vé**, không nhận thêm tham số nào: nếu nhận thêm thì lại phải kiểm tra tham
số có khớp với quyền không, và đó chính là chỗ dễ sinh lỗ hổng. Vì không có gì để khai sai, kết quả luôn
là kết luận của máy chủ.

**Vé dùng lại được nhiều lần trong 10 phút.** Khung chat chỉ được cấp vé **một lần** lúc mở, nhưng người
dùng hỏi nhiều câu liên tiếp; chặn dùng lại sẽ khiến mọi câu thứ hai trở đi thất bại trong khi vé còn hạn.
Cửa sổ rủi ro được giữ hẹp bằng TTL ngắn + xoá vé khỏi thanh địa chỉ ngay khi đọc.

### 6.3. Phân quyền phạm vi (fail-closed)

| Vai trò | `ResolveScope` | Được xem |
| :--- | :--- | :--- |
| Admin · HR · IT | `(true, wholeHotel, null)` | Toàn khách sạn, cả 6 báo cáo |
| Manager **có** claim phòng ban | `(true, false, deptId)` | Chỉ bộ phận mình |
| Manager **thiếu** claim phòng ban | `Denied` | Không gì (thà chặn nhầm) |
| Employee / vai trò lạ | `Denied` | Không gì |

Ngoài `ResolveScope`, endpoint còn kiểm **`PermissionCodes.ReportView`** — đúng cổng quyền mà giao diện
đang dùng — để web và API không bao giờ lệch luật.

**Quyền bổ sung cho từng khối:**

| Khối | Điều kiện xuất hiện |
| :--- | :--- |
| `BienDong`, `CoCau`, `HocVan`, `ChuyenCan`, `SuKien` | Có `ResolveScope.IsAllowed` + `REPORT_VIEW` |
| `QuyLuong` (10.4) | Thêm `CanViewPayrollFundReport` → chỉ **Admin / HR**; Manager **không nhận khoá này** |
| `CaNhan` | Scope `my-payslip` + claim `emp > 0` |

### 6.4. Bảng trạng thái HTTP

| Mã | Khi nào |
| :--- | :--- |
| `200` | Số liệu đã lọc theo phạm vi |
| `400` | Thiếu vé / scope không hợp lệ |
| `401` | Vé sai chữ ký, hết hạn, sai audience, jti lạ, hoặc tài khoản đã bị khoá |
| `403` | Vai trò không được xem báo cáo / Manager thiếu claim phòng ban / thiếu `emp` |
| `503` | Chưa lấy được dữ liệu — **cố ý không trả số 0 kèm theo** |

**Kiểm tài khoản tại thời điểm truy vấn:** vé sống 10 phút, nhưng tài khoản có thể bị khoá trong khoảng đó.
`IsAccountActiveAsync` đọc lại bảng `tai_khoan` mỗi lần gọi, không tin trạng thái lúc phát vé.

### 6.5. Cấu trúc `AiDataDigestDto`

Bản rút gọn **cố ý không trả `BaoCaoViewModel`**: view model mang cả chuỗi dữ liệu biểu đồ, danh mục
phòng ban và cờ giao diện — thừa với mô hình ngôn ngữ và làm phình ngữ cảnh.

```text
AiDataDigestDto
├── ScopeLabel        "Toàn khách sạn" | "Bộ phận: Lễ tân"
├── PeriodLabel       "Tháng 10/2026"
├── RoleLabel         "Quản lý"
├── GeneratedAt       thời điểm dựng
├── GhiChu[]          ghi chú phương pháp mô hình phải nhắc lại
├── BienDong?         10.1 — DauKy, CuoiKy, TuyenMoi, ThoiViec, BienDongRong,
│                            TyLeThoiViec, TheoPhongBan[]
├── CoCau?            10.2 — TongNhanSu, DangLam, NghiPhep, DaThoiViec,
│                            TheoPhongBan[], TheoGioiTinh[], TheoChucVu[]
├── HocVan?           10.3 — TyLeDaiHocTroLen, ThamNienTrungBinh,
│                            TheoTrinhDo[], TheoThamNien[]
├── QuyLuong?         10.4 — ThucChiThucNhan, ThucChiGop, DinhMucThamChieu,
│                            ChenhLech, TyLeGiaiNgan, VuotNguong,
│                            SoThangTrongKy, TheoPhongBan[]   ← CHỈ Admin/HR
├── ChuyenCan?        10.5 — TongLuot, DungGio, DiTre, VeSom, Vang,
│                            TyLeChuyenCan, TongPhutTre, TongPhutVeSom,
│                            TongGioOt, TheoPhongBan[]
├── SuKien?           10.6 — TongSuKien, DangDienRa, DaHoanThanh,
│                            TongLuotPhanCong, TongNganSach, TongPhuCapDaPhanBo,
│                            TyLeXuatSac, DanhSach[]
└── CaNhan?           NgayCongThucTe, NgayNghiPhep, PhieuLuong[3 kỳ gần nhất]
```

**Nguyên tắc "vắng mặt, không phải số 0":** mọi khoá đều `nullable` + `JsonIgnoreCondition.WhenWritingNull`.
Mục không được phép xem **biến mất khỏi JSON** thay vì trả object toàn số 0. Điều này khớp với luật số 2
trong `DATA_SYSTEM_PROMPT`: "Nếu một mục KHÔNG xuất hiện trong JSON, nghĩa là người hỏi không có quyền xem".

**Phạm vi phiếu lương cá nhân:**

- `PersonalPayslipCount = 3` kỳ gần nhất — đủ thấy xu hướng, không phình ngữ cảnh.
- **Mã nhân viên LUÔN lấy từ claim `emp` của vé**, không bao giờ từ tham số yêu cầu. Nhận mã từ thân
  yêu cầu sẽ biến endpoint thành công cụ tra lương người khác.
- Kỳ: năm trong `[2000, 2200]`, tháng `clamp(1..12)`, mặc định là tháng hiện tại.

**Ưu tiên kỳ báo cáo (`ResolvePeriodKind`):** `Quý > Tháng > Năm`. Client gửi kèm cả ba thì Web Admin lấy
mức **chi tiết nhất** — nhất quán với `ReportPolicy.ResolvePeriodRange`.

### 6.6. Ma trận kiểm chứng hỏi–đáp theo quyền (chạy tay 19/10/2026)

Toàn bộ đường đi được đo **bằng chứng thực nghiệm**, không phải bằng đọc mã: đăng nhập từng vai trò trên
cổng Web (`admin`, `hr_sang`, `mgr_long`, `emp_bao`), lấy vé từ chính khung chat nhúng, rồi hỏi qua
`/api/chat` của Next.js. Kết quả dưới đây là câu trả lời **thật** của trợ lý.

| # | Vai trò | Câu hỏi | Kết quả |
| :--- | :--- | :--- | :--- |
| 1 | Manager | "Quỹ lương tháng này của khách sạn?" | Từ chối đúng lý do phạm vi; **không** nói "0 đồng", không bịa số; gợi ý liên hệ Admin/HR |
| 2 | HR | "Quỹ lương tháng này của khách sạn?" | Trả đủ 5 chỉ tiêu thật (thực chi gộp 441.528.269,38 đ · giải ngân 16,3 %) kèm ghi chú phương pháp |
| 3 | Manager | "Lương thực nhận tháng này của tôi?" | Đúng phiếu lương của chính họ; nói rõ "không bao gồm phiếu lương của người khác" |
| 4 | Manager | "Bộ phận của tôi có bao nhiêu nhân sự?" | **4** nhân sự, 2 Nam / 2 Nữ, 3 Lễ tân + 1 Trưởng bộ phận |
| 5 | Manager | "Bộ phận An ninh … quỹ lương bộ phận Bếp?" | Từ chối cả hai; nêu lại đúng phạm vi được cấp |
| 6 | Admin | "Tóm tắt quỹ lương và số nhân sự?" | **25** nhân sự toàn khách sạn + quỹ lương đầy đủ |
| 7 | Manager | Vé rác (thay cho vé đã hết hạn 10 phút) | Báo hết phiên và bảo mở lại từ cổng — **không** bịa số |

**Đối chiếu bằng số ở tầng dữ liệu (không qua mô hình):**

```text
                        Admin                    Manager (Tiền sảnh)
scopeLabel              Toàn khách sạn           Bộ phận: Bộ phận Tiền sảnh (FO)
coCau.tongNhanSu        25                       4
coCau.theoPhongBan      tất cả 9 bộ phận         chỉ Tiền sảnh
có khoá quyLuong        ✔                        ✘ (khoá biến mất hẳn khỏi JSON)
```

Con số này khớp đúng thiết kế: `WhenWritingNull` khiến khoá `QuyLuong` **biến mất** chứ không trả `null`
hay `0` — nên mô hình không có gì để đọc nhầm thành con số.

> **Cách chạy lại ma trận:** đăng nhập ở `http://localhost:5065` → mở nút nổi Trợ lý AI (vé chỉ sinh ra
> khi mở từ cổng, không sinh ra khi gõ thẳng `localhost:3000`) → hỏi đúng các câu ở bảng trên.
> Với `emp_bao` (Employee), cổng Web chặn ngay từ đăng nhập nên **không có vé** — đó là lý do dòng
> Employee vắng mặt trong bảng.

---

## 7. `ingestion_graph` — Nạp Tài Liệu

Đơn giản có chủ ý: đây là endpoint nạp, không phải nơi suy luận.

```text
START → ingestDocs → END
```

| Bước | Hành vi |
| :--- | :--- |
| Đọc `docs` từ state | Nếu rỗng và `useSampleDocs = true` → đọc `docsFile` (mặc định `./src/sample_docs.json`) |
| Chuẩn hoá | `reduceDocs([], docs)` — gán `metadata.uuid` cho từng tài liệu, bỏ trùng theo uuid |
| Ghi | `makeIndexingRetriever(config).addDocuments(docs)` → Supabase `documents` hoặc `MemoryVectorStore` |
| Kết thúc | Trả `{ docs: 'delete' }` để xoá tài liệu khỏi state (tránh phình checkpoint) |

> **Vì sao là `makeIndexingRetriever` chứ không phải `makeRetriever`:** bộ truy hồi đọc có nhiều phạm vi
> được bọc trong `ScopedRetriever` (kế thừa `BaseRetriever`, **không** có `addDocuments`). Đường ghi phải
> là một `VectorStoreRetriever` thật, nên hai đường được tách hẳn nhau — không thể lỡ tay đưa bộ đọc
> chỉ-đọc vào lệnh ghi.

**Chọn nơi lưu vector:**

| Điều kiện | Provider |
| :--- | :--- |
| Có `SUPABASE_URL` **và** `SUPABASE_SERVICE_ROLE_KEY` | `supabase` (bảng `documents`, RPC `match_documents`) |
| Thiếu một trong hai | `memory` — `MemoryVectorStore` **dùng chung một instance trong tiến trình**, kèm cảnh báo |

Lưu ý: `MemoryVectorStore` mất sạch khi restart. Đây là chế độ dự phòng cho máy dev, không phải chế độ chạy thật.

---

## 8. Bốn Prompt Hệ Thống

| Prompt | Dùng ở | Ngôn ngữ | Nội dung cốt lõi |
| :--- | :--- | :--- | :--- |
| `ROUTER_SYSTEM_PROMPT` | `checkQueryType` | Anh | Định nghĩa 4 route + quy tắc ưu tiên `payslip` khi mơ hồ |
| `DIRECT_SYSTEM_PROMPT` | `directAnswer` | Việt | Phạm vi 5 nhóm chủ đề + guardrail từ chối + cấm lộ bí mật + luật LaTeX |
| `RESPONSE_SYSTEM_PROMPT` | `generateResponse` | Việt | Ưu tiên `{context}`; thiếu context thì giải thích theo chuẩn nghiệp vụ + nhắc nạp thêm tài liệu |
| `DATA_SYSTEM_PROMPT` | `dataInsight` | Việt | 11 luật, tâm điểm là cấm bịa số và cách xử lý khoá vắng mặt |

**Ba luật xuyên suốt cả bốn prompt:**

1. **Guardrail phạm vi:** từ chối lịch sự các chủ đề ngoài HRM/khách sạn (nấu ăn, thể thao, chính trị…).
2. **Cấm lộ bí mật:** API key, mật khẩu, chuỗi kết nối, jailbreak/prompt injection.
3. **LaTeX giới hạn:** chỉ `$…$` và `$$…$$`; **cấm** `\begin{align|equation|aligned|cases}` vì
   `rehype-katex` trong giao diện không render được các môi trường mở rộng (sẽ hiện chữ thô).

**Chống lộ khoá kỹ thuật (luật 9 của `DATA_SYSTEM_PROMPT`):** mô hình không được viết `vuotNguong: true`
mà phải viết "đã vượt định mức". Người đọc là quản lý khách sạn, không phải lập trình viên.

**Luật 11 — nhận diện phiếu lương cá nhân:** nếu JSON chỉ có `caNhan` (không có `bienDong`, `coCau`,
`quyLuong`), đó là phiếu lương của chính người hỏi. Không được coi việc thiếu các mục báo cáo là lỗi,
và tuyệt đối không suy ra số liệu người khác từ phiếu này.

---

## 9. Mô Hình & Cấu Hình

### 9.1. Nạp mô hình (`shared/utils.ts`)

`loadChatModel(name, temperature = 0.2)` có **hai nhánh**:

| Nhánh | Điều kiện | Ghi chú |
| :--- | :--- | :--- |
| Điểm cuối tuỳ biến | Có `AI_BASE_URL` **hoặc** (`AI_API_KEY` + `AI_MODEL`) | `ChatOpenAI` trỏ tới `AI_BASE_URL`; tự xoá `frequency_penalty` / `presence_penalty` vì Gemini không nhận |
| Nhà cung cấp chuẩn | Không có biến tuỳ biến | `initChatModel`, kiểm tên trong `SUPPORTED_PROVIDERS` (16 provider) |

`maxRetries: 1`, `timeout: 25000` — giới hạn cứng để một lần gọi treo không giữ cả lượt chat.

### 9.2. Biến môi trường (`backend/.env`)

| Biến | Vai trò |
| :--- | :--- |
| `AI_API_KEY` / `OPENAI_API_KEY` | Khoá mô hình (ưu tiên `AI_API_KEY`) |
| `AI_BASE_URL` / `OPENAI_BASE_URL` | Điểm cuối OpenAI-compatible (Gemini, DeepSeek…) |
| `AI_MODEL` | Ghi đè `queryModel` |
| `EMBEDDING_MODEL` | Mặc định `text-embedding-3-small` |
| `SUPABASE_URL` + `SUPABASE_SERVICE_ROLE_KEY` | Vector store thật; thiếu → memory |
| `DATA_API_URL` | Địa chỉ Web Admin cho cầu dữ liệu. **Chỉ đọc từ env**, không bao giờ từ `configurable` |
| `LANGCHAIN_TRACING_V2` / `LANGCHAIN_PROJECT` | Tracing (tuỳ chọn) |

**Vì sao `DATA_API_URL` phải đọc từ env:** `config.configurable` do **trình duyệt** cung cấp. Đọc URL từ đó
sẽ cho phép kẻ tấn công trỏ máy chủ này tới một địa chỉ nội bộ bất kỳ **kèm vé dữ liệu thật** — một lỗ hổng
SSRF. Bỏ trống `DATA_API_URL` thì tính năng hỏi số liệu tự tắt, phần hỏi đáp tài liệu vẫn chạy.

### 9.2b. Biến môi trường (`frontend/.env`)

| Biến | Vai trò |
| :--- | :--- |
| `NEXT_PUBLIC_LANGGRAPH_API_URL` | Địa chỉ LangGraph; **có** tiền tố `NEXT_PUBLIC_` vì trình duyệt cần gọi trực tiếp |
| `LANGGRAPH_RETRIEVAL_ASSISTANT_ID` | Tên assistant hỏi đáp |
| `LANGGRAPH_INGESTION_ASSISTANT_ID` | Tên assistant nạp tài liệu — `/api/ingest` **dùng chính giá trị này**, không còn chuỗi cứng |
| `WEBADMIN_API_URL` | Địa chỉ Web Admin để hỏi quyền kho tri thức. **Không** đặt `NEXT_PUBLIC_`: chỉ máy chủ Next.js đọc, không lộ ra trình duyệt |

Thiếu `WEBADMIN_API_URL` thì chatbot vẫn chạy: chỉ đọc được tài liệu `all` và **không nạp được tệp**.
Đây là mặc định hẹp nhất, cố ý — hỏng cấu hình không được biến thành mở toang quyền.

### 9.3. Cấu hình phía Web Admin

| Khoá | Ý nghĩa |
| :--- | :--- |
| `Chatbot:Url` | Địa chỉ frontend (mặc định `http://localhost:3000`) |
| `Chatbot:AiTokenKey` | Khoá ký vé — **fail-closed**: thiếu thì `Issue` ném lỗi rõ ràng, `Program.cs` cảnh báo lúc khởi động |
| `AI_CONTEXT_TOKEN_KEY` | Biến môi trường thay thế cho khoá trên |

Khoá ký **cố ý không mượn `Jwt:Key`**: `Jwt:Key` thuộc luồng đăng nhập mobile kiểu cũ đang chờ gỡ; dùng
chung khoá sẽ khiến tính năng mới chết theo khi khoá đó bị xoá.

**Khi chưa cấu hình khoá:** `BuildEmbedUrl` bắt `InvalidOperationException` và vẫn nhúng khung chat
**không kèm vé** — người dùng mất phần hỏi số liệu nhưng vẫn hỏi đáp PDF được. Cố ý không chặn cả trang.

---

## 10. Khởi Chạy

```bash
./scripts/start-ai-chatbot.sh     # LangGraph :2024 + Next.js :3000
dotnet run --project src/OvertimeHotel.HRM.WebAdmin   # WebAdmin :5065
```

| Kiểm tra | Kỳ vọng |
| :--- | :--- |
| `http://localhost:2024/info` | LangGraph server sống — trả `{"flags":{"assistants":true}}` |
| `http://localhost:3000` | Trang chat mở, tạo được thread |
| Nút nổi góc phải WebAdmin | Mở `offcanvas`, `iframe` nạp được |
| Hỏi "Tỷ lệ thôi việc tháng này nghĩa là gì?" | Có badge "Phạm vi: …" và số liệu đúng kỳ |
| Nạp PDF bằng tài khoản Admin | Thành công; hiện `scope` trong thông báo kết quả |
| Nạp PDF bằng tài khoản Nhân viên | **403** kèm lời nhắc liên hệ Nhân sự |

> **`/ok` không tồn tại** — endpoint kiểm tra sống là **`GET /info`**; gọi `/ok` trả 404 dù server
> đang chạy tốt. Đừng dùng mã 404 ở đây để kết luận server chết.

Xem thêm `workflows/ai-chatbot.md` cho hướng dẫn cấu hình khoá API và `workflows/build-and-run.md`.

---

## 11. Ma Trận Bảo Mật

| Mối đe doạ | Lớp chặn | Vị trí |
| :--- | :--- | :--- |
| Chatbot đọc CSDL trực tiếp | Không cấp khoá Supabase; chỉ cấp vé đọc | `AiContextTokenService` |
| Dùng vé mobile / cookie phiên cho endpoint AI | Audience riêng `OverTimeHotel.AiData` | `Validate` |
| Tự ký vé bằng khoá đoán được | HS256 + jti phải có trong `IMemoryCache` của chính máy chủ | `Validate` |
| Vé lọt access log / Referer | Vé ở fragment, xoá khỏi địa chỉ ngay khi đọc | `AiChatbotUrlBuilder`, `page.tsx` |
| Nâng quyền bằng `scope` trong thân yêu cầu | Scope do **graph** chọn từ route; Web Admin kiểm lại `ResolveScope` | `loadDataDigest`, `BuildReportsAsync` |
| Tra lương người khác | Mã nhân viên **chỉ** từ claim `emp` | `BuildPersonalAsync` |
| Tra số liệu người khác qua hội thoại | Prompt cấm suy ra từ phiếu lương | `DATA_SYSTEM_PROMPT` luật 11 |
| SSRF tới máy chủ nội bộ | URL chỉ từ `DATA_API_URL` | `loadDataDigest` |
| Tài khoản bị khoá giữa phiên | Đọc lại `tai_khoan` mỗi lần gọi | `IsAccountActiveAsync` |
| Trình duyệt giả `pageContext` | Chỉ ảnh hưởng lời diễn giải, không mang quyền | `AgentConfigurationAnnotation` |
| Lộ khoá kỹ thuật trong câu trả lời | Prompt cấm viết tên khoá JSON | `DATA_SYSTEM_PROMPT` luật 9 |
| Người ngoài nạp tài liệu độc hại vào kho tri thức | `/api/ingest` bắt buộc vé + quyền `QL_TRI_THUC_AI`, trả 403 trước khi đọc tệp | `ingest/route.ts`, `KnowledgeAccessPolicy` |
| Trình duyệt tự khai `docScopes` / `scope` để nâng quyền đọc | Máy chủ quyết lại từ vé; `docScopes` ghi đè sau cùng; scope lạ bị loại | `chat/route.ts`, `normalizeDocScopes` |
| Nhân viên đọc quy chế nội bộ phòng ban khác | Chunk gắn `metadata.scope` lúc nạp; truy hồi lọc theo `all` + `dept:<mình>` | `retrieval.ts`, `resolveDocumentScopes` |
| Dán nhãn tài liệu vào phòng ban không có quyền ghi | `scope` chỉ nhận nếu nằm trong `ingestScopes` do Web Admin dựng | `ingest/route.ts` |
| Web Admin chết ⇒ chatbot mở toang quyền đọc | Mọi nhánh hỏng đều fail-closed về `all`, không bao giờ về `*` | `knowledge-access.ts` |
| Tài khoản bị khoá vẫn đọc được tài liệu nội bộ | `AiKnowledgeAccessService` đọc lại `tai_khoan` mỗi lần | `ResolveAsync` |

---

## 12. Khoảng Trống & Hướng Xây Tiếp

Bốn khoảng trống đầu tiên (#1–#4) đã được xử lý — xem §12.1. Các mục còn lại **chưa có trong mã nguồn**
tại thời điểm viết tài liệu. Ghi ra để không ai mô tả chúng như đã hoàn thành, và để chọn thứ tự ưu tiên
khi xây tiếp.

| # | Khoảng trống | Ảnh hưởng | Hướng xử lý gợi ý |
| :--- | :--- | :--- | :--- |
| ~~1~~ | ~~Tài liệu PDF **không phân quyền**~~ — **Đã xử lý**, xem §12.1 | — | — |
| ~~2~~ | ~~`/api/ingest` **không kiểm vé**~~ — **Đã xử lý**, xem §12.1 | — | — |
| ~~3~~ | ~~`AgentConfiguration` trong `graphTypes.ts` thiếu `year` / `month` / `quarter`~~ — **Đã sửa** | — | — |
| ~~4~~ | ~~`/api/ingest` dùng **chuỗi cứng** `'ingestion_graph'`~~ — **Đã sửa** | — | — |
| 5 | **Đang xảy ra:** `backend/.env` để trống `SUPABASE_URL` + `SUPABASE_SERVICE_ROLE_KEY` → chạy `MemoryVectorStore`, và bảng `public.documents` **chưa tồn tại** | Mất toàn bộ tài liệu khi restart; nạp xong hỏi lại không thấy; backfill `scope` ở §12.1 không chạy được | Điền hai biến môi trường để bật pgvector thật (xem §9.2) |
| 6 | Không có giới hạn **tần suất** gọi mô hình | Chi phí và hàng đợi không được bảo vệ | Thêm rate limit theo vé/người ở `/api/chat` |
| 7 | Router prompt viết tiếng Anh trong khi câu trả lời tiếng Việt | Có thể phân loại lệch với câu hỏi tiếng Việt mơ hồ | Thử nghiệm bản router tiếng Việt, đo bằng bộ câu hỏi mẫu |
| 8 | Lịch sử hội thoại chỉ sống trong thread LangGraph, không có màn hình chọn phiên | Không xem lại được phiên cũ | Dùng `searchThreads`/`getThreadHistory` đã có sẵn trong `langgraph-base.ts` |
| 9 | Chưa có bộ đánh giá chất lượng câu trả lời số liệu | Không đo được mô hình có bịa số hay không | Bộ câu hỏi vàng + đối chiếu với `/api/ai-data/digest` |
| 10 | `sample_docs.json`, `useSampleDocs`, `demo.ts`, `dist/` còn sót từ dự án gốc | Gây nhầm khi đọc mã nguồn | Dọn dẹp hoặc ghi rõ mục đích |
| 11 | **UI thiếu bộ chọn phạm vi khi nạp.** `ingestScopes` và `departments` đã được Web Admin trả về nhưng **không nơi nào trong `page.tsx` đọc tới** | Không nạp được tài liệu vào phòng ban cụ thể; mọi tệp rơi vào `all`. Phần "ghi theo phòng ban" mới xong ở tầng API, chưa có đường dùng | Thêm dropdown chọn `all` / `dept:<n>` từ `access.ingestScopes`, hiển thị tên phòng ban từ `departments` |
| 12 | **Khung nạp không bị ẩn khi thiếu vé.** Mở thẳng `localhost:3000` vẫn thấy đủ nút nạp | Người dùng tưởng mình không có quyền, trong khi thật ra là chưa đăng nhập qua cổng HRM | Đã chặn sớm ở client kèm thông báo phân biệt "chưa có phiên" với "không đủ quyền"; còn lại nên ẩn hẳn khung nạp khi không có vé |

### 12.1. Phân Quyền Kho Tri Thức (đã hoàn thành 19/10/2026)

Mục tiêu: kho tri thức là **trạng thái dùng chung**, nên vừa phải chặn người ngoài ghi vào, vừa phải
không để nhân viên phòng này đọc quy chế nội bộ phòng khác — mà **không** phải sửa hàm `match_documents`
trong CSDL. Cách làm: gắn nhãn phạm vi lên từng chunk lúc nạp, rồi để bộ truy hồi lọc theo nhãn đó.

#### Mã phạm vi (scope)

| Mã | Nghĩa | Ai ghi được | Ai đọc được |
| :--- | :--- | :--- | :--- |
| `all` | Tài liệu dùng chung toàn khách sạn | HR, IT, Admin | Tất cả vai trò |
| `dept:<mã phòng ban>` | Tài liệu riêng của một phòng ban | HR, IT, Admin | Nhân viên/Quản lý **thuộc đúng phòng đó**, cộng HR/IT/Admin |
| `*` | Toàn bộ kho tri thức (không lọc) | — (chỉ nội bộ, không nhận từ trình duyệt) | HR, IT, Admin |

Nhãn được ghi vào `metadata.scope` của **mọi chunk**, kèm `metadata.uploadedAt` (ISO-8601 UTC) và
`metadata.uploadedBy` (mã tài khoản) để truy vết nguồn gốc tệp.

#### Bốn thay đổi chính

1. **`/api/ingest` bắt buộc vé.** Vé dữ liệu đi kèm trong trường `ticket` của multipart body (không phải
   header, không phải URL — không lọt access log). Next.js hỏi ngược Web Admin qua
   `POST /api/ai-data/knowledge-access`; không có quyền nạp thì trả **403** trước khi đọc bất kỳ byte PDF nào.
2. **Quyền nạp tách riêng khỏi quyền đọc báo cáo.** Mã quyền mới `QL_TRI_THUC_AI`, cấp cho vai trò
   **HR (2)** và **IT (5)**; Admin đi qua `IsInRole("Admin")`. Cố ý **không** cấp cho Quản lý (3) và
   Nhân viên (4). Migration: `20261019_them_quyen_tri_thuc_ai.sql`.
3. **Truy hồi theo phòng ban.** `makeRetriever` chạy **một lượt tìm cho mỗi scope** rồi hợp nhất bằng
   Reciprocal Rank Fusion (`RRF_K = 60`, khoá theo `metadata.uuid`). Không đụng vào `match_documents`:
   `SupabaseVectorStore` đã tự chuyển object `filterKwargs` thành đối số `filter` của RPC, và RPC dùng
   containment `metadata @> filter`.
4. **Quyền đọc do máy chủ quyết, không do trình duyệt khai.** `KnowledgeAccessPolicy` (thuần quy tắc,
   không chạm CSDL) + `AiKnowledgeAccessService` (đọc lại `tai_khoan` xem còn hoạt động không) trả về
   `documentScopes` / `ingestScopes` / `departments`. `chat/route.ts` ghi đè `docScopes` **sau cùng**
   trong object `configurable`, nên giá trị lọt từ body trình duyệt không thể thắng.

#### Fail-closed ở mọi nhánh hỏng

| Tình huống | Kết quả |
| :--- | :--- |
| Không có vé (hoặc vé hết hạn, sai audience) | Chỉ đọc `all`; không nạp được |
| `WEBADMIN_API_URL` chưa cấu hình | Chỉ đọc `all`; không nạp được |
| Web Admin trả lỗi / quá 5 giây | Chỉ đọc `all`; không nạp được |
| Vé hợp lệ nhưng tài khoản đã bị khoá | Chỉ đọc `all`; không nạp được |
| Trình duyệt gửi scope lạ (`dept:999`, chuỗi rác) | Bị loại bỏ; nạp vào `all` |
| `docScopes` không phải mảng hoặc rỗng | Ép về `['all']` trong `normalizeDocScopes` |

#### Hai việc phải làm tay khi triển khai

> **Bắt buộc chạy tay migration** `20261019_them_quyen_tri_thuc_ai.sql` trên SQL Editor của Supabase
> trước khi dùng tính năng nạp tài liệu. Seed chỉ chạy khi cài mới; CSDL đang có sẽ thiếu dòng quyền
> `QL_TRI_THUC_AI` và vì thế **HR/IT sẽ bị từ chối nạp** (Admin vẫn qua được nhờ `IsInRole`).

> **Tài liệu nạp TRƯỚC ngày 19/10/2026 không có `metadata.scope`** nên sẽ **không xuất hiện** trong
> bất kỳ lượt truy hồi có lọc nào — tức là biến mất khỏi tầm nhìn của mọi vai trò, kể cả người đã nạp.
> Đây là hệ quả của việc lọc theo `metadata @> {scope}`: bản ghi không có khoá thì không khớp.
> Gán `all` cho toàn bộ tài liệu cũ để chúng trở lại là tài liệu dùng chung:
>
> ```sql
> -- Chạy MỘT LẦN, chỉ khi kho đã có tài liệu từ trước migration này.
> -- Đặt scope = all vì trước đây mọi tài liệu vốn đã dùng chung cho tất cả vai trò —
> -- gán lại đúng bằng "dùng chung" nên không mở rộng cũng không thu hẹp quyền của ai.
> UPDATE public.documents
>    SET metadata = metadata || '{"scope": "all"}'::jsonb
>  WHERE metadata ->> 'scope' IS NULL;
> ```
>
> Nếu muốn siết lại: thay `all` bằng `dept:<mã>` cho những tệp thuộc riêng một phòng ban, hoặc để
> nguyên `NULL` cho tệp không còn dùng nữa — chúng sẽ tự rời khỏi kho.
>
> **Lệnh trên chỉ có tác dụng khi đã bật vector store thật.** Hiện `backend/.env` để trống
> `SUPABASE_URL` và `SUPABASE_SERVICE_ROLE_KEY`, nên chatbot đang chạy `MemoryVectorStore` và **bảng
> `public.documents` chưa tồn tại** — chạy lệnh này sẽ báo `relation "public.documents" does not
> exist`. Đó là khoảng trống #5 (mục 12), không phải lỗi phân quyền. Điền hai biến môi trường trước.

> **Biến môi trường bắt buộc:** `WEBADMIN_API_URL=http://localhost:5065` trong `frontend/.env`.
> Đặt **không** có tiền tố `NEXT_PUBLIC_` — giá trị này chỉ dùng phía máy chủ Next.js.

---

## 13. Bảng Đối Chiếu Tệp → Trách Nhiệm

| Tệp | Trách nhiệm duy nhất |
| :--- | :--- |
| `backend/src/retrieval_graph/graph.ts` | Toàn bộ định tuyến, tra cứu, gọi cầu dữ liệu, sinh câu trả lời |
| `backend/src/retrieval_graph/state.ts` | Hợp đồng dữ liệu giữa các node |
| `backend/src/retrieval_graph/configuration.ts` | Đọc & làm sạch cấu hình do trình duyệt gửi |
| `backend/src/retrieval_graph/prompts.ts` | Bốn prompt; **nơi duy nhất** định nghĩa guardrail |
| `backend/src/shared/retrieval.ts` | Chọn Supabase hay memory, dựng bộ truy hồi theo phạm vi (RRF), tách đường ghi |
| `backend/src/shared/utils.ts` | Chọn mô hình theo env, giới hạn retry/timeout |
| `frontend/app/page.tsx` | Đọc vé/ngữ cảnh, gửi câu hỏi, ráp SSE vào giao diện |
| `frontend/lib/knowledge-access.ts` | Hỏi Web Admin "vé này đọc/nạp được gì"; **mọi nhánh hỏng trả về `all`** |
| `frontend/app/api/chat/route.ts` | Proxy SSE, ghi đè `docScopes` từ máy chủ xuống graph |
| `frontend/app/api/ingest/route.ts` | Đòi vé + quyền, kiểm file, gắn `scope`, chạy ingestion graph |
| `frontend/components/chat-message.tsx` | Markdown + KaTeX + nguồn tài liệu |
| WebAdmin `Security/AiContextTokenService.cs` | Ký và xác minh vé |
| WebAdmin `Security/KnowledgeAccessPolicy.cs` | Quy tắc thuần (không CSDL): vai trò nào đọc/nạp được phạm vi nào |
| WebAdmin `Security/AiKnowledgeAccessService.cs` | Ghép quy tắc với dữ liệu sống; đọc lại `tai_khoan`; fail-closed |
| WebAdmin `Security/AiChatbotUrlBuilder.cs` | Dựng URL nhúng + ngữ cảnh + vé |
| WebAdmin `Controllers/AiAssistantController.cs` | Trang và điểm nhúng |
| WebAdmin `Controllers/Api/AiDataController.cs` | Hai cửa: `digest` (số liệu) và `knowledge-access` (kho tri thức) |
| WebAdmin `Models/AiDataDigestDto.cs` | Hợp đồng JSON với mô hình; "vắng mặt, không phải số 0" |
| `Views/Shared/_Layout.cshtml` | Drawer nổi dùng chung mọi trang |
| `Views/AiAssistant/Index.cshtml` | Trang trợ lý riêng |
| `supabase/migrations/20261019_them_quyen_tri_thuc_ai.sql` | Cấp quyền `QL_TRI_THUC_AI` cho HR (2) và IT (5) |
