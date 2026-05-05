import liff from '@line/liff'
import { useCallback, useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { env } from '@/shared/config/env'
import type { ApiResponse } from '@/shared/types/api'
import { Button } from '@/shared/ui/Button'

type Phase = 'loading' | 'needLineApp' | 'needFriend' | 'submitting' | 'done' | 'error'

const liffId = import.meta.env.VITE_LINE_LIFF_ID as string | undefined

export const LineNotifyLiffPage = () => {
  const [searchParams] = useSearchParams()
  const bindToken = searchParams.get('bindToken') ?? ''
  const botLink = searchParams.get('botLink') ?? ''

  const [phase, setPhase] = useState<Phase>('loading')
  const [errorText, setErrorText] = useState<string | null>(null)

  const postComplete = useCallback(async () => {
    const idToken = await liff.getIDToken()
    const res = await fetch(`${env.apiBaseUrl}/api/v1/account/line/bind/liff-complete`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ bindingToken: bindToken, idToken }),
    })
    const json = (await res.json()) as ApiResponse<{ bound: boolean }>
    if (!json.success) {
      throw new Error(json.error.message)
    }
  }, [bindToken])

  const finishAndClose = useCallback(async () => {
    setPhase('done')
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
      await postComplete()
      await finishAndClose()
    } catch (err) {
      setPhase('error')
      setErrorText(err instanceof Error ? err.message : '綁定失敗')
    }
  }, [finishAndClose, postComplete])

  const refreshFriendship = useCallback(async () => {
    const f = await liff.getFriendship()
    if (f.friendFlag) {
      await runComplete()
    } else {
      setPhase('needFriend')
    }
  }, [runComplete])

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
          <Button type="button" variant="secondary" className="w-full" onClick={() => void refreshFriendship()}>
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
