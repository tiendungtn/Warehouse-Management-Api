namespace QuanLyKho.DTOs;

public sealed record DashboardTrendPointDto(
    int Day,
    decimal Revenue,
    decimal PreviousRevenue,
    int ReceiptCount,
    int PreviousReceiptCount,
    int IssueCount,
    int PreviousIssueCount
);

public sealed record DashboardAnalyticsDto(
    DateTime CurrentMonth,
    DateTime PreviousMonth,
    IReadOnlyList<DashboardTrendPointDto> Trends
);