import type { PropsWithChildren } from 'react'

type CardProps = PropsWithChildren<{
  className?: string
}>

export const Card = ({ children, className }: CardProps) => {
  return <section className={`rounded-2xl border border-border bg-surface p-5 shadow-soft ${className ?? ''}`}>{children}</section>
}
