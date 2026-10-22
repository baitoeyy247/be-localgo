namespace LocalGo.Application.Common;

public static class ApiProblem
{
    public static (string Type, string Title) ForStatus(int statusCode) => statusCode switch
    {
        400 => ("https://localgo.local/errors/bad-request", "คำขอไม่ถูกต้อง"),
        403 => ("https://localgo.local/errors/forbidden", "ไม่มีสิทธิ์"),
        404 => ("https://localgo.local/errors/not-found", "ไม่พบข้อมูล"),
        409 => ("https://localgo.local/errors/conflict", "ไม่สามารถดำเนินการได้"),
        422 => ("https://localgo.local/errors/validation", "ข้อมูลไม่ถูกต้อง"),
        429 => ("https://localgo.local/errors/rate-limited", "คำขอมากเกินไป"),
        _ => ("https://localgo.local/errors/internal", "เกิดข้อผิดพลาด"),
    };
}
