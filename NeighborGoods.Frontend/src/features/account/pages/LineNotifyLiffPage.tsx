import liff from '@line/liff'
import { useCallback, useEffect, useRef, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { env } from '@/shared/config/env'
import { unwrapApiResponse, type ApiResponse } from '@/shared/types/api'
import { Button } from '@/shared/ui/Button'

type Phase = 'loading' | 'needLineApp' | 'needFriend' | 'submitting' | 'done' | 'error'

const liffId = import.meta.env.VITE_LINE_LIFF_ID as string | undefined
const LINE_BINDING_COMPLETED_FLAG = 'neighborGoods.lineBindingCompleted'

export const LineNotifyLiffPage = () => {
  const [searchParams] = useSearchParams()
  const liffStateRaw = searchParams.get('liff.state') ?? ''
  const liffStateParams = liffStateRaw
    ? new URLSearchParams(liffStateRaw.startsWith('?') ? liffStateRaw.slice(1) : liffStateRaw)
    : null
  const bindToken = searchParams.get('bindToken') ?? liffStateParams?.get('bindToken') ?? ''
  const botLink = searchParams.get('botLink') ?? liffStateParams?.get('botLink') ?? ''

  const [phase, setPhase] = useState<Phase>('loading')
  const [errorText, setErrorText] = useState<string | null>(null)
  const isCheckingFriendshipRef = useRef(false)

  const postComplete = useCallback(async (): Promise<boolean> => {
    const idToken = liff.getIDToken()
    if (!idToken) {
      liff.login({ redirectUri: window.location.href })
      return false
    }

    const res = await fetch(`${env.apiBaseUrl}/api/v1/account/line/bind/liff-complete`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ bindingToken: bindToken, idToken }),
    })
    const json = (await res.json()) as ApiResponse<{ bound: boolean }>
    unwrapApiResponse(json)
    return true
  }, [bindToken])

  const finishAndClose = useCallback(async () => {
    setPhase('done')
    sessionStorage.setItem(LINE_BINDING_COMPLETED_FLAG, '1')
    const accountUrl = `${window.location.origin}/account?lineBound=1`
    if (liff.isInClient()) {
      try {
        await liff.closeWindow()
        return
      } catch {
        // fall through to redirect
      }
    }

    window.location.replace(accountUrl)
  }, [])

  const runComplete = useCallback(async () => {
    setPhase('submitting')
    setErrorText(null)
    try {
      const completed = await postComplete()
      if (!completed) {
        setPhase('loading')
        return
      }
      await finishAndClose()
    } catch (err) {
      setPhase('error')
      setErrorText(err instanceof Error ? err.message : '綁定失敗')
    }
  }, [finishAndClose, postComplete])

  const refreshFriendship = useCallback(async () => {
    if (isCheckingFriendshipRef.current) {
      return
    }

    isCheckingFriendshipRef.current = true
    try {
      if (!liff.isApiAvailable('getFriendship')) {
        setPhase('needFriend')
        return
      }

      const f = await liff.getFriendship()
      if (f.friendFlag) {
        await runComplete()
      } else {
        setPhase('needFriend')
      }
    } catch {
      setPhase('needFriend')
    } finally {
      isCheckingFriendshipRef.current = false
    }
  }, [runComplete])

  const handleConfirmFriend = useCallback(() => {
    if (liff.isApiAvailable('getFriendship')) {
      void refreshFriendship()
      return
    }

    void runComplete()
  }, [refreshFriendship, runComplete])

  useEffect(() => {
    let disposed = false

    void (async () => {
      if (!liffId?.trim()) {
        setPhase('error')
        setErrorText('VITE_LINE_LIFF_ID 未設定，無法完成 LIFF 綁定。')
        return
      }

      if (!bindToken) {
        setPhase('error')
        setErrorText('缺少綁定參數，請至網站「我的帳號」重新開始綁定。')
        return
      }

      try {
        await liff.init({ liffId: liffId.trim() })
        if (disposed) {
          return
        }

        if (!liff.isInClient()) {
          setPhase('needLineApp')
          return
        }

        if (!liff.isLoggedIn()) {
          liff.login({ redirectUri: window.location.href })
          return
        }

        await refreshFriendship()
      } catch (err) {
        if (!disposed) {
          setPhase('error')
          setErrorText(err instanceof Error ? err.message : 'LIFF 初始化失敗')
        }
      }
    })()

    return () => {
      disposed = true
    }
  }, [bindToken, refreshFriendship])

  useEffect(() => {
    if (phase !== 'needFriend') {
      return
    }

    const autoResume = () => {
      if (document.visibilityState === 'visible') {
        void refreshFriendship()
      }
    }

    window.addEventListener('focus', autoResume)
    document.addEventListener('visibilitychange', autoResume)
    return () => {
      window.removeEventListener('focus', autoResume)
      document.removeEventListener('visibilitychange', autoResume)
    }
  }, [phase, refreshFriendship])

  const handleOpenAddFriend = () => {
    if (!botLink) {
      setErrorText('缺少加好友連結，請回網站重新取得綁定連結。')
      return
    }

    void liff.openWindow({ url: botLink, external: false })
  }

  return (
    <main className="mx-auto flex min-h-[50vh] max-w-md flex-col justify-center gap-4 px-4 py-8">
      <h1 className="text-xl font-semibold text-text-main">LINE 官方通知綁定</h1>

      {phase === 'loading' ? <p className="text-text-subtle">載入中…</p> : null}

      {phase === 'needLineApp' ? (
        <p className="text-text-subtle">請在 LINE App 內開啟此連結，以完成綁定。</p>
      ) : null}

      {phase === 'needFriend' ? (
        <div className="space-y-3">
          <p className="text-text-subtle">請先加入 NeighborGoods 官方帳號為好友，才能接收推播通知。</p>
          <Button type="button" className="w-full" onClick={() => void handleOpenAddFriend()}>
            開啟加好友
          </Button>
          <Button type="button" variant="secondary" className="w-full" onClick={() => void handleConfirmFriend()}>
            我已完成加好友
          </Button>
        </div>
      ) : null}

      {phase === 'submitting' ? <p className="text-text-subtle">綁定中…</p> : null}

      {phase === 'done' ? <p className="text-[#2F7D4E]">綁定成功，您可以關閉此畫面。</p> : null}

      {phase === 'error' && errorText ? (
        <div className="space-y-3">
          <p className="text-danger">{errorText}</p>
          <Button
            type="button"
            variant="secondary"
            className="w-full"
            onClick={() => {
              setErrorText(null)
              void runComplete()
            }}
          >
            重試完成綁定
          </Button>
        </div>
      ) : null}
    </main>
  )
}
