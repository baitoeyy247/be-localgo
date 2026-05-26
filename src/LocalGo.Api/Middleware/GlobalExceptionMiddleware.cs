using System.Net;
using System.Text.Json;
using LocalGo.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace LocalGo.Api.Middleware;

public sealed class GlobalExceptionMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionMiddleware> logger,
    IHostEnvironment environment)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (AppException ex)
        {
            await WriteProblemAsync(context, (HttpStatusCode)ex.StatusCode, ex.Message);
        }
        catch (OperationCanceledException ex) when (context.RequestAborted.IsCancellationRequested)
        {
            logger.LogDebug(ex, "Request cancelled by client. {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteProblemAsync(context, (HttpStatusCode)499, "คำขอถูกยกเลิก");
        }
        catch (TaskCanceledException ex)
        {
            logger.LogWarning(ex, "Request timed out or was cancelled. {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteProblemAsync(context, HttpStatusCode.RequestTimeout, "คำขอใช้เวลานานเกินไป ลองใหม่อีกครั้ง");
        }
        catch (DbUpdateException ex) when (IsDuplicateKey(ex))
        {
            logger.LogWarning(ex, "Duplicate key conflict. {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteProblemAsync(
                context,
                HttpStatusCode.Conflict,
                "มีข้อมูลนี้อยู่แล้วในระบบ ลองรีเฟรชหน้าแล้วทำรายการใหม่");
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex, "Database update failed. {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteProblemAsync(
                context,
                HttpStatusCode.Conflict,
                "ไม่สามารถบันทึกข้อมูลได้ ลองรีเฟรชหน้าแล้วทำรายการใหม่",
                ex);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "JSON serialization failed. {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteProblemAsync(
                context,
                HttpStatusCode.InternalServerError,
                "ไม่สามารถส่งข้อมูลกลับได้",
                ex);
        }
        catch (ArgumentException ex) when (IsJsonNumberException(ex))
        {
            logger.LogError(ex, "Invalid numeric value in API response. {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteProblemAsync(
                context,
                HttpStatusCode.InternalServerError,
                "ไม่สามารถส่งข้อมูลกลับได้",
                ex);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Unhandled exception. {Method} {Path} TraceId={TraceId}",
                context.Request.Method,
                context.Request.Path,
                context.TraceIdentifier);
            await WriteProblemAsync(
                context,
                HttpStatusCode.InternalServerError,
                "เกิดข้อผิดพลาดในระบบ ลองใหม่อีกครั้ง",
                ex);
        }
    }

    private static bool IsDuplicateKey(DbUpdateException ex)
    {
        for (var inner = ex.InnerException; inner is not null; inner = inner.InnerException)
        {
            var message = inner.Message;
            if (message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
                || message.Contains("23505", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsJsonNumberException(ArgumentException ex) =>
        ex.Message.Contains("cannot be written as valid JSON", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("JsonNumberHandling", StringComparison.OrdinalIgnoreCase);

    private async Task WriteProblemAsync(
        HttpContext context,
        HttpStatusCode status,
        string detail,
        Exception? ex = null)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)status;

        var resolvedDetail = detail;
        if (environment.IsDevelopment() && ex is not null)
        {
            var inner = ex.InnerException?.Message ?? ex.Message;
            if (!string.IsNullOrWhiteSpace(inner) && !string.Equals(resolvedDetail, inner, StringComparison.Ordinal))
            {
                resolvedDetail = $"{detail} ({inner})";
            }
        }

        var problem = new
        {
            type = "https://localgo.local/errors/internal",
            title = "Server error",
            status = (int)status,
            detail = resolvedDetail,
            traceId = context.TraceIdentifier,
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
    }
}
