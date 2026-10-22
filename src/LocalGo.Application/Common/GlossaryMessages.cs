namespace LocalGo.Application.Common;

/// <summary>
/// User-facing Thai copy mirrored from fe-localgo/src/i18n/glossary.ts (N-7).
/// </summary>
public static class GlossaryMessages
{
    public const string WithdrawBid = "ถอนข้อเสนอ";
    public const string BidWithdrawn = "ถอนข้อเสนอแล้ว";
    public const string CancelAppointment = "ยกเลิกนัดหมาย";
    public const string BudgetOpenChoice = "ให้ผู้ให้บริการเสนอราคา";
    public const string JobStatusOpenOffers = "เปิดรับข้อเสนอ";

    public const string OpenBudgetCanonical = "เปิดรับข้อเสนอ";
    public const string OpenBudgetLegacy = "เปิดรับราคา";

    public const string BudgetRequired = "กรุณาระบุงบประมาณหรือเลือกเปิดรับข้อเสนอ";
    public const string BudgetInvalid =
        "งบประมาณต้องเป็นจำนวนเต็มตั้งแต่ 1 ถึง 1000000 หรือเปิดรับข้อเสนอ";

    public const string WithdrawBidInvalidStatus = "ถอนข้อเสนอได้เฉพาะข้อเสนอที่ส่งแล้วเท่านั้น";
    public const string CancelAppointmentInvalidStatus = "ยกเลิกนัดหมายไม่ได้ในสถานะนี้";
}
