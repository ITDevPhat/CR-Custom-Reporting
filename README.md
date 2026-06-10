# ASP.NET + Next.js Semantic Report Builder

Hướng dẫn mở và chạy dự án semantic report builder gồm backend ASP.NET và frontend Next.js.

## Cấu trúc thư mục

```text
ASP.NET/
├─ ReportPlatform/          Backend ASP.NET
│  ├─ Report.Api/           API project
│  ├─ Report.Contracts/     Request/response contracts
│  ├─ Report.Metadata/      Semantic metadata, registries
│  ├─ Report.QueryEngine/   Query planner + SQL compiler
│  └─ Report.QueryEngine.Tests/
└─ data-report-builder/     Frontend Next.js
```

## Yêu cầu môi trường

- .NET SDK phù hợp với project hiện tại
- Node.js
- pnpm
- SQL Server nếu muốn dùng flow Connect Source thật

Kiểm tra nhanh:

```powershell
dotnet --version
node --version
pnpm --version
```

## Chạy backend

Mở terminal tại thư mục:

```powershell
cd D:\ITDevPhat\ASP.NET\ReportPlatform
```

Restore/build:

```powershell
dotnet restore
dotnet build
```

Chạy API:

```powershell
dotnet run --project Report.Api --launch-profile http
```

Backend sẽ chạy tại:

```text
http://localhost:5224
```

Một số endpoint chính:

```text
GET  /api/datasets/{datasetId}/metadata
POST /api/query/compile
POST /api/query/execute
POST /api/connections/test
POST /api/connections/discover
POST /api/connections/preview-table
POST /api/datasets/register-from-tables
```

## Chạy frontend

Mở terminal khác tại thư mục:

```powershell
cd D:\ITDevPhat\ASP.NET\data-report-builder
```

Cài package nếu cần:

```powershell
pnpm install
```

Chạy Next.js:

```powershell
pnpm dev
```

Frontend mặc định sẽ chạy tại:

```text
http://localhost:3000
```

Frontend đang dùng file:

```text
data-report-builder/.env.local
```

với cấu hình:

```env
NEXT_PUBLIC_REPORT_API_URL=http://localhost:5224
```

Nếu đổi port backend, sửa lại biến này rồi restart frontend.

## Chạy test backend

Từ thư mục:

```powershell
cd D:\ITDevPhat\ASP.NET\ReportPlatform
```

Chạy toàn bộ test:

```powershell
dotnet test
```

Test SQL generation nằm ở:

```text
ReportPlatform/Report.QueryEngine.Tests
```

Suite này kiểm tra luồng:

```text
VisualQueryRequest
→ SemanticModelBinder
→ EvaluationContextBuilder
→ RelationshipTraversalEngine
→ MeasureExpansionEngine
→ LogicalPlanBuilder
→ SqlCompiler
```

## Cách mở bằng Visual Studio

1. Mở Visual Studio.
2. Chọn `Open a project or solution`.
3. Chọn file:

```text
D:\ITDevPhat\ASP.NET\ReportPlatform\ReportPlatform.slnx
```

4. Set `Report.Api` làm startup project.
5. Chạy profile `http`.

Frontend vẫn chạy riêng bằng `pnpm dev`.

## Cách mở bằng VS Code

Mở root workspace:

```powershell
code D:\ITDevPhat\ASP.NET
```

Terminal 1 chạy backend:

```powershell
cd ReportPlatform
dotnet run --project Report.Api --launch-profile http
```

Terminal 2 chạy frontend:

```powershell
cd data-report-builder
pnpm dev
```

## Luồng sử dụng nhanh

1. Chạy backend tại `http://localhost:5224`.
2. Chạy frontend tại `http://localhost:3000`.
3. Mở frontend trong browser.
4. Dùng `Connect Source` để kết nối SQL Server.
5. Test connection.
6. Discover tables.
7. Chọn tables và Load.
8. Data Fields sẽ reload từ backend metadata.
9. Kéo field/metric vào report.
10. Bấm `Run Report`.

## Ghi chú SQL Server

Flow Connect Source hiện tập trung SQL Server.

Với SQL authentication, cần thông tin dạng:

```text
Server
Database
Username
Password
Encrypt
Trust Server Certificate
```

Mật khẩu chỉ phục vụ kết nối/runtime MVP, không trả về frontend trong response.

## Lệnh hay dùng

Backend:

```powershell
cd D:\ITDevPhat\ASP.NET\ReportPlatform
dotnet build
dotnet test
dotnet run --project Report.Api --launch-profile http
```

Frontend:

```powershell
cd D:\ITDevPhat\ASP.NET\data-report-builder
pnpm install
pnpm dev
pnpm build
pnpm exec tsc --noEmit
```

