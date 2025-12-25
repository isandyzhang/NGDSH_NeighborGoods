-- 手動執行此 SQL 腳本來添加已讀時間欄位
-- 如果使用 Entity Framework 遷移，請執行: dotnet ef database update

ALTER TABLE [Conversations]
ADD [Participant1LastReadAt] [datetime2] NULL;

ALTER TABLE [Conversations]
ADD [Participant2LastReadAt] [datetime2] NULL;

