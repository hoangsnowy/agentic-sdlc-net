// AgenticSdlc.Web/Services/Demo/DemoLlmClient.cs
// Phase 7 — Nguồn LLM "canned" cho chế độ Demo offline. KHÔNG gọi mạng: nhận biết agent
// đang gọi qua system prompt rồi trả JSON hợp lệ theo schema, đồng thời mô phỏng Quality Loop
// (QA fail vòng đầu → pass vòng sau) để buổi bảo vệ thấy được cơ chế lặp.
//
// Lưu ý: client này scoped theo circuit nên bộ đếm vòng lặp độc lập giữa các phiên người dùng.
// Một lần chạy pipeline là tuần tự (orchestrator await từng agent) nên các bộ đếm an toàn.

using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AgenticSdlc.Domain.Llm;
using AgenticSdlc.Infrastructure.Llm;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AgenticSdlc.Web.Services.Demo;

/// <summary>
/// Impl <see cref="ILlmClient"/> trả dữ liệu mẫu deterministic cho demo offline.
/// Phân biệt agent bằng dòng mở đầu system prompt ("Bạn là … Agent").
/// </summary>
public sealed class DemoLlmClient : ILlmClient
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly int _stepDelayMs;
    private readonly int _failingQaRounds;
    private readonly ILogger<DemoLlmClient> _logger;

    // Bộ đếm theo circuit — reset khi bắt đầu một story mới (lần gọi Requirement).
    private int _qaCalls;
    private int _codingCalls;

    /// <inheritdoc />
    public string Provider => "Demo";

    /// <summary>Khởi tạo, đọc tham số tốc độ + số vòng QA fail từ section <c>Demo</c>.</summary>
    public DemoLlmClient(IConfiguration configuration, ILogger<DemoLlmClient> logger)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _stepDelayMs = configuration.GetValue("Demo:StepDelayMs", 650);
        _failingQaRounds = Math.Max(0, configuration.GetValue("Demo:FailingQaRounds", 1));
    }

    /// <inheritdoc />
    public async Task<LlmResponse> SendAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        var sw = Stopwatch.StartNew();
        if (_stepDelayMs > 0)
        {
            await Task.Delay(_stepDelayMs, cancellationToken).ConfigureAwait(false);
        }

        var sys = request.SystemPrompt ?? string.Empty;
        string content;
        int inTok, outTok;

        // Khớp theo DÒNG ĐỊNH DANH "Bạn là <X> Agent" — không khớp suffix trần "<X> Agent"
        // vì prompt khác có nhắc chéo (vd Testing prompt nhắc "Coding Agent") gây route nhầm.
        if (Contains(sys, "Bạn là Requirement Agent"))
        {
            _qaCalls = 0;
            _codingCalls = 0;
            content = RequirementJson(request.UserPrompt);
            (inTok, outTok) = (420, 360);
        }
        else if (Contains(sys, "Bạn là Coding Agent"))
        {
            _codingCalls++;
            content = CodeJson(_codingCalls);
            (inTok, outTok) = (680, _codingCalls > 1 ? 1240 : 1180);
        }
        else if (Contains(sys, "Bạn là Testing Agent"))
        {
            content = TestJson();
            (inTok, outTok) = (640, 760);
        }
        else if (Contains(sys, "Bạn là QA Agent"))
        {
            _qaCalls++;
            var pass = _qaCalls > _failingQaRounds;
            content = QaJson(pass);
            (inTok, outTok) = (880, pass ? 240 : 320);
        }
        else
        {
            content = "{}";
            (inTok, outTok) = (10, 2);
            _logger.LogWarning("DemoLlmClient: không nhận diện được agent từ system prompt.");
        }

        sw.Stop();
        var cost = CostCalculator.Calculate(request.Model, inTok, outTok);
        return new LlmResponse(content, inTok, outTok, cost, sw.Elapsed, request.Model, Provider);
    }

    private static bool Contains(string s, string marker)
        => s.Contains(marker, StringComparison.OrdinalIgnoreCase);

    // ---------------- Canned payloads ----------------

    private static string RequirementJson(string userPrompt)
    {
        var snippet = Snippet(userPrompt, 140);
        var obj = new
        {
            title = "Quản lý sản phẩm trong danh mục",
            summary = $"Cho phép quản trị viên tạo, cập nhật, tìm kiếm sản phẩm với SKU duy nhất. (User story: {snippet})",
            stakeholders = new[] { "Quản trị viên", "Khách hàng" },
            functionalRequirements = new[]
            {
                "Tạo sản phẩm mới với SKU duy nhất",
                "Cập nhật thông tin và giá sản phẩm",
                "Xoá mềm (soft-delete) sản phẩm",
                "Tìm kiếm sản phẩm theo tên và phân trang",
            },
            nonFunctionalRequirements = new[]
            {
                "Thời gian phản hồi p95 ≤ 200ms",
                "Ghi log mọi thao tác ghi dữ liệu",
            },
            entities = new[]
            {
                new
                {
                    name = "Product",
                    fields = new[] { "Id: Guid", "Sku: string", "Name: string", "Price: decimal", "IsActive: bool" },
                    notes = "SKU phải duy nhất trên toàn hệ thống",
                },
            },
            endpoints = new[]
            {
                new { method = "POST", path = "/products", purpose = "Tạo sản phẩm mới", authRequired = true },
                new { method = "GET", path = "/products/{id}", purpose = "Lấy chi tiết sản phẩm", authRequired = false },
                new { method = "GET", path = "/products", purpose = "Danh sách + tìm kiếm có phân trang", authRequired = false },
            },
            acceptanceCriteria = new[]
            {
                "Tạo sản phẩm trùng SKU phải trả lỗi 409 Conflict",
                "Lấy sản phẩm không tồn tại trả 404 Not Found",
                "Danh sách hỗ trợ phân trang với pageSize tối đa 100",
            },
        };
        return JsonSerializer.Serialize(obj, Json);
    }

    private static string CodeJson(int iteration)
    {
        var fixedNote = iteration > 1
            ? "Vòng tái sinh: đã bổ sung phân trang cho GET /products và kiểm tra SKU trùng theo feedback QA."
            : "Bản sinh đầu tiên theo Clean Architecture (Domain / Application / Api).";

        var obj = new
        {
            projectName = "ProductCatalog",
            architecture = "Clean Architecture",
            files = new[]
            {
                new
                {
                    path = "src/Domain/Product.cs",
                    content =
                        "namespace ProductCatalog.Domain;\n\n" +
                        "public sealed class Product\n{\n" +
                        "    public Guid Id { get; init; } = Guid.NewGuid();\n" +
                        "    public required string Sku { get; init; }\n" +
                        "    public required string Name { get; set; }\n" +
                        "    public decimal Price { get; set; }\n" +
                        "    public bool IsActive { get; set; } = true;\n}\n",
                    language = "csharp",
                },
                new
                {
                    path = "src/Application/IProductRepository.cs",
                    content =
                        "namespace ProductCatalog.Application;\n\n" +
                        "public interface IProductRepository\n{\n" +
                        "    Task<bool> SkuExistsAsync(string sku, CancellationToken ct);\n" +
                        "    Task AddAsync(Domain.Product product, CancellationToken ct);\n" +
                        "    Task<IReadOnlyList<Domain.Product>> SearchAsync(string? q, int page, int size, CancellationToken ct);\n}\n",
                    language = "csharp",
                },
                new
                {
                    path = "src/Api/ProductEndpoints.cs",
                    content =
                        "namespace ProductCatalog.Api;\n\n" +
                        "public static class ProductEndpoints\n{\n" +
                        "    public static void MapProducts(this IEndpointRouteBuilder app)\n    {\n" +
                        "        app.MapPost(\"/products\", async (CreateProduct cmd, IProductRepository repo, CancellationToken ct) =>\n" +
                        "        {\n" +
                        "            if (await repo.SkuExistsAsync(cmd.Sku, ct))\n" +
                        "                return Results.Conflict($\"SKU {cmd.Sku} đã tồn tại.\");\n" +
                        "            var p = new Domain.Product { Sku = cmd.Sku, Name = cmd.Name, Price = cmd.Price };\n" +
                        "            await repo.AddAsync(p, ct);\n" +
                        "            return Results.Created($\"/products/{p.Id}\", p);\n" +
                        "        });\n    }\n}\n\n" +
                        "public sealed record CreateProduct(string Sku, string Name, decimal Price);\n",
                    language = "csharp",
                },
            },
            notes = fixedNote,
        };
        return JsonSerializer.Serialize(obj, Json);
    }

    private static string TestJson()
    {
        var obj = new
        {
            framework = "xUnit",
            files = new[]
            {
                new
                {
                    path = "tests/ProductEndpointsTests.cs",
                    content =
                        "using Shouldly;\nusing Xunit;\n\n" +
                        "public class ProductEndpointsTests\n{\n" +
                        "    [Fact]\n" +
                        "    public async Task CreateProduct_NewSku_Returns201()\n    {\n" +
                        "        var repo = new FakeRepo(skuExists: false);\n" +
                        "        var result = await CreateHandler.Run(new(\"SKU-1\", \"Bàn\", 100m), repo);\n" +
                        "        result.ShouldBeOfType<Created<Product>>();\n    }\n\n" +
                        "    [Fact]\n" +
                        "    public async Task CreateProduct_DuplicateSku_Returns409()\n    {\n" +
                        "        var repo = new FakeRepo(skuExists: true);\n" +
                        "        var result = await CreateHandler.Run(new(\"SKU-1\", \"Bàn\", 100m), repo);\n" +
                        "        result.ShouldBeOfType<Conflict<string>>();\n    }\n\n" +
                        "    [Theory]\n    [InlineData(-1)]\n    [InlineData(0)]\n" +
                        "    public async Task CreateProduct_InvalidPrice_Throws(decimal price)\n    {\n" +
                        "        var repo = new FakeRepo(skuExists: false);\n" +
                        "        await Should.ThrowAsync<ArgumentException>(\n" +
                        "            () => CreateHandler.Run(new(\"SKU-2\", \"Ghế\", price), repo));\n    }\n}\n",
                    language = "csharp",
                },
            },
            happyPathCount = 1,
            edgeCaseCount = 2,
            errorCaseCount = 1,
            estimatedCoveragePercent = 78,
        };
        return JsonSerializer.Serialize(obj, Json);
    }

    private static string QaJson(bool pass)
    {
        object obj = pass
            ? new
            {
                score = 0.92,
                isConsistent = true,
                iterationNeeded = false,
                issues = new[]
                {
                    new
                    {
                        severity = "Minor",
                        category = "CodeQuality",
                        description = "Nên thêm validation FluentValidation cho CreateProduct (không bắt buộc).",
                        location = "src/Api/ProductEndpoints.cs",
                    },
                },
                recommendations = Array.Empty<string>(),
            }
            : new
            {
                score = 0.62,
                isConsistent = false,
                iterationNeeded = true,
                issues = new[]
                {
                    new
                    {
                        severity = "Major",
                        category = "RequirementCoverage",
                        description = "Endpoint GET /products chưa hiện thực phân trang theo acceptance criteria.",
                        location = "src/Api/ProductEndpoints.cs",
                    },
                    new
                    {
                        severity = "Major",
                        category = "TestCoverage",
                        description = "Thiếu test cho phân trang và giới hạn pageSize ≤ 100.",
                        location = "tests/ProductEndpointsTests.cs",
                    },
                },
                recommendations = new[]
                {
                    "Bổ sung tham số page/size cho GET /products và clamp size ≤ 100.",
                    "Thêm test phân trang vào ProductEndpointsTests.",
                },
            };
        return JsonSerializer.Serialize(obj, Json);
    }

    private static string Snippet(string? text, int max)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "(không có)";
        }
        var t = text.Trim().ReplaceLineEndings(" ");
        return t.Length <= max ? t : string.Concat(t.AsSpan(0, max), "…");
    }
}
