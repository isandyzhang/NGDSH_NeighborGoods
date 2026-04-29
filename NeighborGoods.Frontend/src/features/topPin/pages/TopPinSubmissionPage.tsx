import { Link } from 'react-router-dom'
import { Card } from '@/shared/ui/Card'

export const TopPinSubmissionPage = () => {
  return (
    <main className="mx-auto w-full max-w-3xl px-4 py-6 md:py-8">
      <section className="mb-6 space-y-2 text-center">
        <p className="text-sm uppercase tracking-[0.18em] text-text-subtle">NeighborGoods</p>
        <h1 className="text-4xl font-semibold leading-tight text-text-main sm:text-5xl">置頂投稿</h1>
        <p className="text-base text-text-subtle">投稿通過審核後，平台會依分類安排置頂曝光與推播。</p>
      </section>

      <Card className="space-y-4">
        <h2 className="text-xl font-semibold text-text-main">投稿說明</h2>
        <ul className="list-disc space-y-1 pl-5 text-text-subtle">
          <li>提供商品亮點、用途與適合對象，能提高審核通過率。</li>
          <li>投稿內容需與商品資訊一致，且遵守平台交易規範。</li>
          <li>審核結果會透過站內通知告知。</li>
        </ul>
        <p className="text-sm text-text-muted">目前先提供投稿入口頁，後續可再擴充正式投稿表單。</p>
        <div>
          <Link to="/my-listings" className="text-sm text-brand hover:underline">
            返回我的商品
          </Link>
        </div>
      </Card>
    </main>
  )
}
