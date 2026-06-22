using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace VectorRAGvsPageIndexRAG.Tools.PdfGenerator;

public class TechnicalReport
{
    public void Generate(string outputPath)
    {
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var boldFont = builder.AddStandard14Font(Standard14Font.HelveticaBold);
        var monoFont = builder.AddStandard14Font(Standard14Font.Courier);

        // Title page
        var page = builder.AddPage(PageSize.A4);
        AddText(page, "CloudSync API Documentation", 24, boldFont, 50, 700);
        AddText(page, "Version 3.2.1", 14, font, 50, 660);
        AddText(page, "Last updated: June 2026", 12, font, 50, 640);
        AddText(page, "Contact: api-support@cloudsync.io", 12, font, 50, 620);

        // Section 1: Authentication
        page = builder.AddPage(PageSize.A4);
        AddText(page, "1. Authentication", 18, boldFont, 50, 750);
        AddText(page, "1.1 API Keys", 14, boldFont, 50, 720);
        AddText(page, "All API requests require authentication via API key. Include your key in the Authorization header:", 11, font, 50, 700);
        AddText(page, "Authorization: Bearer your-api-key-here", 10, monoFont, 70, 680);
        AddText(page, "API keys are scoped to your organization and can be revoked at any time from the dashboard.", 11, font, 50, 660);

        AddText(page, "1.2 OAuth 2.0", 14, boldFont, 50, 630);
        AddText(page, "For server-to-server authentication, we recommend OAuth 2.0 with client credentials flow.", 11, font, 50, 610);
        AddText(page, "Token endpoint: https://auth.cloudsync.io/oauth/token", 10, monoFont, 70, 590);
        AddText(page, "Rate limit: 1000 requests per minute per client ID", 11, font, 50, 570);

        // Section 2: REST Endpoints
        page = builder.AddPage(PageSize.A4);
        AddText(page, "2. REST API Endpoints", 18, boldFont, 50, 750);

        AddText(page, "2.1 Files", 14, boldFont, 50, 720);
        AddText(page, "GET    /api/v3/files              - List all files", 10, monoFont, 70, 700);
        AddText(page, "POST   /api/v3/files              - Upload a file", 10, monoFont, 70, 685);
        AddText(page, "GET    /api/v3/files/{id}          - Get file metadata", 10, monoFont, 70, 670);
        AddText(page, "DELETE /api/v3/files/{id}          - Delete a file", 10, monoFont, 70, 655);
        AddText(page, "GET    /api/v3/files/{id}/content  - Download file content", 10, monoFont, 70, 640);

        AddText(page, "2.2 Folders", 14, boldFont, 50, 610);
        AddText(page, "GET    /api/v3/folders            - List all folders", 10, monoFont, 70, 590);
        AddText(page, "POST   /api/v3/folders            - Create a folder", 10, monoFont, 70, 575);
        AddText(page, "GET    /api/v3/folders/{id}        - Get folder contents", 10, monoFont, 70, 560);
        AddText(page, "PUT    /api/v3/folders/{id}        - Update folder", 10, monoFont, 70, 545);
        AddText(page, "DELETE /api/v3/folders/{id}        - Delete folder", 10, monoFont, 70, 530);

        AddText(page, "2.3 Sharing", 14, boldFont, 50, 500);
        AddText(page, "POST   /api/v3/shares              - Create a share link", 10, monoFont, 70, 480);
        AddText(page, "GET    /api/v3/shares/{id}         - Get share details", 10, monoFont, 70, 465);
        AddText(page, "DELETE /api/v3/shares/{id}         - Revoke share link", 10, monoFont, 70, 450);

        // Section 3: WebSocket
        page = builder.AddPage(PageSize.A4);
        AddText(page, "3. WebSocket API", 18, boldFont, 50, 750);
        AddText(page, "3.1 Connection", 14, boldFont, 50, 720);
        AddText(page, "Connect to: wss://ws.cloudsync.io/v3/events", 10, monoFont, 70, 700);
        AddText(page, "Connection timeout: 30 seconds", 11, font, 50, 680);
        AddText(page, "Idle timeout: 5 minutes (sends ping every 60s)", 11, font, 50, 660);
        AddText(page, "Max message size: 1MB", 11, font, 50, 640);

        AddText(page, "3.2 Authentication", 14, boldFont, 50, 610);
        AddText(page, "Send auth message immediately after connection:", 11, font, 50, 590);
        AddText(page, "{\"type\": \"auth\", \"token\": \"your-api-key\"}", 10, monoFont, 70, 570);
        AddText(page, "Server responds with {\"type\": \"auth_ok\"} on success.", 11, font, 50, 550);

        AddText(page, "3.3 Events", 14, boldFont, 50, 520);
        AddText(page, "file.created    - New file uploaded", 11, font, 70, 500);
        AddText(page, "file.updated    - File content modified", 11, font, 70, 485);
        AddText(page, "file.deleted    - File removed", 11, font, 70, 470);
        AddText(page, "folder.created  - New folder created", 11, font, 70, 455);
        AddText(page, "share.created   - Share link created", 11, font, 70, 440);
        AddText(page, "share.revoked   - Share link revoked", 11, font, 70, 425);

        // Section 4: Rate Limiting
        page = builder.AddPage(PageSize.A4);
        AddText(page, "4. Rate Limiting", 18, boldFont, 50, 750);
        AddText(page, "4.1 Limits by Plan", 14, boldFont, 50, 720);
        AddText(page, "Free tier:       100 requests/minute", 11, font, 70, 700);
        AddText(page, "Pro tier:        1,000 requests/minute", 11, font, 70, 685);
        AddText(page, "Enterprise tier: 10,000 requests/minute", 11, font, 70, 670);
        AddText(page, "Premium tier:    50,000 requests/minute", 11, font, 70, 655);

        AddText(page, "4.2 Rate Limit Headers", 14, boldFont, 50, 620);
        AddText(page, "X-RateLimit-Limit: Maximum requests per window", 11, font, 70, 600);
        AddText(page, "X-RateLimit-Remaining: Requests remaining", 11, font, 70, 585);
        AddText(page, "X-RateLimit-Reset: Unix timestamp when window resets", 11, font, 70, 570);

        AddText(page, "4.3 Exceeded Limits", 14, boldFont, 50, 540);
        AddText(page, "Returns HTTP 429 Too Many Requests with Retry-After header.", 11, font, 70, 520);
        AddText(page, "Backoff exponentially: 1s, 2s, 4s, 8s, 16s, max 60s.", 11, font, 70, 505);

        // Section 5: Error Codes
        page = builder.AddPage(PageSize.A4);
        AddText(page, "5. Error Codes", 18, boldFont, 50, 750);
        AddText(page, "400  Bad Request          - Invalid request parameters", 11, font, 70, 720);
        AddText(page, "401  Unauthorized         - Missing or invalid API key", 11, font, 70, 705);
        AddText(page, "403  Forbidden            - Insufficient permissions", 11, font, 70, 690);
        AddText(page, "404  Not Found            - Resource does not exist", 11, font, 70, 675);
        AddText(page, "409  Conflict             - Resource already exists", 11, font, 70, 660);
        AddText(page, "413  Payload Too Large    - File exceeds 100MB limit", 11, font, 70, 645);
        AddText(page, "429  Too Many Requests    - Rate limit exceeded", 11, font, 70, 630);
        AddText(page, "500  Internal Server Error - Server-side error", 11, font, 70, 615);
        AddText(page, "503  Service Unavailable  - Maintenance or overload", 11, font, 70, 600);

        // Section 6: SDK Reference
        page = builder.AddPage(PageSize.A4);
        AddText(page, "6. SDK Reference", 18, boldFont, 50, 750);
        AddText(page, "6.1 Official SDKs", 14, boldFont, 50, 720);
        AddText(page, "Python:   pip install cloudsync-python (v3.2.0)", 11, font, 70, 700);
        AddText(page, "Node.js:  npm install @cloudsync/sdk (v3.2.1)", 11, font, 70, 685);
        AddText(page, "Java:     com.cloudsync:sdk:3.2.0", 11, font, 70, 670);
        AddText(page, "Go:       go get github.com/cloudsync/sdk-go/v3", 11, font, 70, 655);
        AddText(page, "Ruby:     gem install cloudsync (v3.2.0)", 11, font, 70, 640);
        AddText(page, "C#:       Install-Package CloudSync.SDK (v3.2.1)", 11, font, 70, 625);
        AddText(page, "PHP:      composer require cloudsync/sdk (v3.2.0)", 11, font, 70, 610);
        AddText(page, "Swift:    .package(url: \"https://github.com/cloudsync/sdk-swift\")", 11, font, 70, 595);

        AddText(page, "6.2 Quick Start (Python)", 14, boldFont, 50, 560);
        AddText(page, "from cloudsync import CloudSync", 10, monoFont, 70, 540);
        AddText(page, "", 10, monoFont, 70, 525);
        AddText(page, "client = CloudSync(api_key=\"your-key\")", 10, monoFont, 70, 525);
        AddText(page, "files = client.files.list(folder_id=\"root\")", 10, monoFont, 70, 510);
        AddText(page, "for file in files:", 10, monoFont, 70, 495);
        AddText(page, "    print(f\"{file.name} ({file.size} bytes)\")", 10, monoFont, 70, 480);

        // Section 7: Webhooks
        page = builder.AddPage(PageSize.A4);
        AddText(page, "7. Webhooks", 18, boldFont, 50, 750);
        AddText(page, "7.1 Configuration", 14, boldFont, 50, 720);
        AddText(page, "Webhooks deliver real-time notifications to your server via HTTP POST.", 11, font, 50, 700);
        AddText(page, "Endpoint must respond with 2xx within 5 seconds.", 11, font, 50, 680);
        AddText(page, "Failed deliveries retry 3 times with exponential backoff.", 11, font, 50, 660);

        AddText(page, "7.2 Payload Format", 14, boldFont, 50, 630);
        AddText(page, "{\"event\": \"file.created\", \"timestamp\": \"...\",", 10, monoFont, 70, 610);
        AddText(page, " \"data\": {\"id\": \"...\", \"name\": \"...\"}}", 10, monoFont, 70, 595);

        AddText(page, "7.3 Signature Verification", 14, boldFont, 50, 560);
        AddText(page, "X-CloudSync-Signature: HMAC-SHA256 of payload with your webhook secret", 11, font, 70, 540);
        AddText(page, "Always verify signatures to prevent spoofed requests.", 11, font, 50, 520);

        // Section 8: Pagination
        page = builder.AddPage(PageSize.A4);
        AddText(page, "8. Pagination", 18, boldFont, 50, 750);
        AddText(page, "All list endpoints support cursor-based pagination.", 11, font, 50, 720);
        AddText(page, "Query parameters:", 11, font, 50, 700);
        AddText(page, "  limit    - Items per page (default: 20, max: 100)", 11, font, 70, 685);
        AddText(page, "  cursor   - Pagination cursor from previous response", 11, font, 70, 670);
        AddText(page, "  sort     - Sort field (name, created_at, size)", 11, font, 70, 655);
        AddText(page, "  order    - Sort order (asc, desc)", 11, font, 70, 640);

        AddText(page, "Response includes:", 11, font, 50, 610);
        AddText(page, "  data[]       - Array of items", 11, font, 70, 595);
        AddText(page, "  next_cursor  - Cursor for next page (null if no more)", 11, font, 70, 580);
        AddText(page, "  has_more     - Boolean indicating more pages", 11, font, 70, 565);

        // Appendix
        page = builder.AddPage(PageSize.A4);
        AddText(page, "Appendix A: Changelog", 18, boldFont, 50, 750);
        AddText(page, "v3.2.1 (June 2026)", 12, boldFont, 50, 720);
        AddText(page, "- Fixed WebSocket reconnection bug", 11, font, 70, 700);
        AddText(page, "- Added webhook signature verification", 11, font, 70, 685);
        AddText(page, "- Improved rate limit headers", 11, font, 70, 670);

        AddText(page, "v3.2.0 (May 2026)", 12, boldFont, 50, 640);
        AddText(page, "- Added WebSocket API for real-time events", 11, font, 70, 620);
        AddText(page, "- New sharing endpoints with expiration", 11, font, 70, 605);
        AddText(page, "- SDK support for Go and Swift", 11, font, 70, 590);

        AddText(page, "v3.1.0 (March 2026)", 12, boldFont, 50, 560);
        AddText(page, "- Added folder management endpoints", 11, font, 70, 540);
        AddText(page, "- Improved error messages", 11, font, 70, 525);
        AddText(page, "- Added pagination support", 11, font, 70, 510);

        var bytes = builder.Build();
        File.WriteAllBytes(outputPath, bytes);
    }

    private static void AddText(PdfPageBuilder page, string text, double fontSize,
        PdfDocumentBuilder.AddedFont font, double x, double y)
    {
        page.AddText(text, fontSize, new PdfPoint(x, y), font);
    }
}
