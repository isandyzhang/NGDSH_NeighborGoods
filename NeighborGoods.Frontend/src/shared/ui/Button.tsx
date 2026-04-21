import type { ButtonHTMLAttributes, PropsWithChildren } from 'react'

type ButtonProps = PropsWithChildren<
  ButtonHTMLAttributes<HTMLButtonElement> & {
    variant?: 'primary' | 'secondary'
    fullWidth?: boolean
  }
>

export const Button = ({
  children,
  className,
  variant = 'primary',
  fullWidth = false,
  ...props
}: ButtonProps) => {
  const baseClasses =
    'rounded-xl px-4 py-2 text-sm font-medium transition disabled:cursor-not-allowed disabled:opacity-60'

  const variantClasses =
    variant === 'primary'
      ? 'bg-brand text-brand-foreground hover:bg-brand-strong'
      : 'border border-border bg-surface text-text-main hover:bg-surface-2'

  return (
    <button
      className={`${baseClasses} ${variantClasses} ${fullWidth ? 'w-full' : ''} ${className ?? ''}`}
      {...props}
    >
      {children}
    </button>
  )
}
