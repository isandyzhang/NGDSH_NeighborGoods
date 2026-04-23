import { Button } from '@/shared/ui/Button'
import { Card } from '@/shared/ui/Card'

type ErrorStateProps = {
  title?: string
  description: string
  onRetry?: () => void
}

export const ErrorState = ({ title = '發生錯誤', description, onRetry }: ErrorStateProps) => {
  return (
    <Card className="text-center">
      <p className="text-xl font-semibold text-danger">{title}</p>
      <p className="mt-2 text-base text-text-subtle">{description}</p>
      {onRetry ? (
        <Button type="button" variant="secondary" className="mt-4 min-h-[2.8rem] px-5 text-base font-semibold" onClick={onRetry}>
          重試
        </Button>
      ) : null}
    </Card>
  )
}
