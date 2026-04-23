import { Card } from '@/shared/ui/Card'

export const PrivacyPage = () => {
  return (
    <main className="mx-auto w-full max-w-4xl px-4 py-6 md:py-8">
      <section className="mb-8 space-y-3 text-center">
        <p className="text-sm uppercase tracking-[0.18em] text-text-subtle">NeighborGoods</p>
        <h1 className="text-5xl font-semibold leading-tight text-text-main sm:text-6xl md:text-7xl">
          隱私<span className="marker-wipe">條款</span>
        </h1>
        <p className="mx-auto max-w-2xl text-lg text-text-subtle">
          我們重視你的個人資料，以下說明資料蒐集與使用方式。
        </p>
      </section>

      <Card className="space-y-5">
        <p className="text-lg leading-8 text-text-main">
          本平台會蒐集帳號識別資料、刊登內容與訊息互動資料，用於提供交易服務、帳號安全維護與功能通知。
        </p>
        <ul className="list-disc space-y-3 pl-6 text-lg leading-8 text-text-main">
          <li>僅在提供服務必要範圍內蒐集與使用資料。</li>
          <li>不會任意對外公開個人資料，除非依法配合主管機關要求。</li>
          <li>你可以透過個人設定頁更新基本資料與通知偏好。</li>
          <li>為維護帳號安全，系統可能保留必要的操作紀錄與登入紀錄。</li>
          <li>若你停用帳號或提出刪除需求，我們將依規範處理可刪除之資料。</li>
        </ul>
      </Card>
    </main>
  )
}
