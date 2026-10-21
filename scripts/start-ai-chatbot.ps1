# Script khởi chạy toàn bộ phân hệ AI Chatbot (Backend LangGraph Port 2024 & Frontend Next.js Port 3000)
$root = Resolve-Path "$PSScriptRoot\..\src\OvertimeHotel.HRM.AiChatbot"
Write-Host "Starting AI Chatbot Backend (LangGraph Port 2024)..." -ForegroundColor Cyan
Start-Process node -ArgumentList "--env-file=backend/.env --import tsx -e `"import { startServer } from './node_modules/@langchain/langgraph-cli/dist/server.mjs'; await startServer({ port: 2024, nWorkers: 1, host: 'localhost', cwd: process.cwd() + '/backend', graphs: { ingestion_graph: './src/ingestion_graph/graph.ts:graph', retrieval_graph: './src/retrieval_graph/graph.ts:graph' } });`"" -WorkingDirectory $root

Start-Sleep -Seconds 2

Write-Host "Starting AI Chatbot Frontend (Next.js Port 3000)..." -ForegroundColor Green
Start-Process node -ArgumentList "../node_modules/next/dist/bin/next dev -p 3000" -WorkingDirectory "$root\frontend"

Write-Host "AI Chatbot services started successfully!" -ForegroundColor Yellow
Write-Host "Backend:  http://localhost:2024"
Write-Host "Frontend: http://localhost:3000"
Write-Host "WebAdmin: http://localhost:5065/AiAssistant"
