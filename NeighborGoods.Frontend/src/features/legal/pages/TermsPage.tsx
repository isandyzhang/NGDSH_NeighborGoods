import { Card } from '@/shared/ui/Card'

export const TermsPage = () => {
  return (
    <main className="mx-auto w-full max-w-4xl px-4 py-6 md:py-8">
      <section className="mb-8 space-y-3 text-center">
        <p className="text-sm uppercase tracking-[0.18em] text-text-subtle">NeighborGoods</p>
        <h1 className="text-5xl font-semibold leading-tight text-text-main sm:text-6xl md:text-7xl">
          使用<span className="marker-wipe">條款</span>
        </h1>
        <p className="mx-auto max-w-2xl text-lg text-text-subtle">
          使用本平台前，請先閱讀並同意以下條款內容。
        </p>
      </section>

      <Card className="space-y-5">
        <p className="text-lg leading-8 text-text-main">
          本平台為社區住戶二手交易服務，僅提供商品資訊刊登、瀏覽與訊息聯繫，不參與買賣雙方實際交易流程。
        </p>
        <ul className="list-disc space-y-3 pl-6 text-lg leading-8 text-text-main">
          <li>交易請以面交為主，並自行確認商品狀態、數量與內容。</li>
          <li>請勿在平台進行匯款交易、包裹寄送或其他高風險方式。</li>
          <li>買賣雙方請自行約定地點與時間，並準時赴約。</li>
          <li>平台不負責處理交易糾紛，請雙方自行協調解決。</li>
          <li>若有違法、詐騙或不當使用行為，平台可限制或終止帳號使用權限。</li>
        </ul>
      </Card>
    </main>
  )
}
