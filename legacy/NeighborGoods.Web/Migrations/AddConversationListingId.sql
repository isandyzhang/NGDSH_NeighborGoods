-- 手動執行此 SQL 腳本來添加 ListingId 欄位
-- 如果使用 Entity Framework 遷移，請執行: dotnet ef database update

-- 注意：此腳本會刪除所有現有對話（因為所有對話都必須關聯商品）
-- 如果資料庫中已有重要對話資料，請先備份

-- 刪除所有現有對話
DELETE FROM [Conversations];

-- 添加 ListingId 欄位（不可為空）
ALTER TABLE [Conversations]
ADD [ListingId] [uniqueidentifier] NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

-- 建立外鍵關聯
CREATE INDEX [IX_Conversations_ListingId] ON [Conversations] ([ListingId]);

ALTER TABLE [Conversations]
ADD CONSTRAINT [FK_Conversations_Listings_ListingId] 
FOREIGN KEY ([ListingId]) REFERENCES [Listings]([Id]) ON DELETE NO ACTION;

-- 更新索引以包含 ListingId
DROP INDEX [IX_Conversations_Participant1Id_Participant2Id] ON [Conversations];

CREATE INDEX [IX_Conversations_Participant1Id_Participant2Id_ListingId] 
ON [Conversations] ([Participant1Id], [Participant2Id], [ListingId]);

-- 在遷移歷史表中記錄此遷移（讓 EF Core 知道遷移已執行）
IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20251226130000_AddConversationListingId')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES ('20251226130000_AddConversationListingId', '8.0.10');
END

