import { Card } from '@/shared/ui/Card'

export const PageSkeleton = ({ className }: { className?: string }) => {
  return <Card className={`animate-pulse bg-surface-2 ${className ?? 'h-56'}`} />
}
