import { useNavigate } from 'react-router-dom'
import { markFirstListingSellerWelcomeSeen } from '@/features/listings/constants/firstListingSellerWelcome'
import { AppModal } from '@/shared/ui/modal/AppModal'
import { Button } from '@/shared/ui/Button'

type FirstListingSellerWelcomeModalProps = {
  open: boolean
  onAcknowledge: () => void
  onClose: () => void
}

export const FirstListingSellerWelcomeModal = ({
  open,
  onAcknowledge,
  onClose,
}: FirstListingSellerWelcomeModalProps) => {
  const navigate = useNavigate()

  const handleAcknowledge = () => {
    markFirstListingSellerWelcomeSeen()
    onAcknowledge()
  }

  const handleGoBindLine = () => {
    markFirstListingSellerWelcomeSeen()
    onClose()
    navigate('/account')
  }

  const handleBackdropClose = () => {
    markFirstListingSellerWelcomeSeen()
    onClose()
  }

  return (
    <AppModal open={open} onClose={handleBackdropClose} closeLabel="關閉賣家提醒" maxWidthClassName="max-w-lg">
      <div className="space-y-4">
        <h2 className="text-2xl font-bold leading-snug text-text-main">
          賣家你好，歡迎刊登！(ﾉ◕ヮ◕)ﾉ*:･ﾟ✧
        </h2>

        <div className="space-y-3 text-base leading-relaxed text-text-subtle">
          <p>嗨，新賣家你好～ (´▽`)</p>
          <p>
            商品刊登成功後，請盡量<strong className="font-semibold text-text-main">確實回覆好厝邊的訊息</strong>
            。若未讀訊息累積過多，為保障買家權益，管理者可能會
            <strong className="font-semibold text-text-main">暫停該帳號</strong>使用權限。
          </p>
          <p>
            也歡迎你綁定並使用 <strong className="font-semibold text-text-main">LINE 官方帳號</strong>
            ，讓訊息不漏接、回覆更即時 (๑•̀ㅂ•́)و✧
          </p>
          <p>
            若商品已在其他地方售出，也請記得回來
            <strong className="font-semibold text-text-main">更新商品狀態</strong>
            ，避免鄰居們白等一場。
          </p>
          <p>你一定也不希望大家期待落空、心一直懸著放不下吧？對吧？對吧？(｡•́︿•̀｡)♡</p>
        </div>

        <div className="flex flex-col gap-2 pt-1">
          <Button type="button" fullWidth className="min-h-[3rem] text-base font-semibold" onClick={handleAcknowledge}>
            我知道了，會好好回覆！(ง •̀_•́)ง
          </Button>
          <Button
            type="button"
            variant="secondary"
            fullWidth
            className="min-h-[3rem] text-base font-semibold"
            onClick={handleGoBindLine}
          >
            前往綁定 LINE
          </Button>
        </div>
      </div>
    </AppModal>
  )
}
