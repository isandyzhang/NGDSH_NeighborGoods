import type { HTMLAttributes, PropsWithChildren } from 'react'

type CardProps = PropsWithChildren<{
  className?: string
}> &
  HTMLAttributes<HTMLElement>

export const Card = ({ children, className, ...props }: CardProps) => {
  return (
    <section
      className={`rounded-2xl border border-border bg-surface p-5 shadow-soft ${className ?? ''}`}
      {...props}
    >
      {children}
    </section>
  )
}
