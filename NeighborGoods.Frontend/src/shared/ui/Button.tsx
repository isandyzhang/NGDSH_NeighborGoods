import type { ButtonHTMLAttributes, PropsWithChildren } from 'react'

export type ButtonVariant = 'primary' | 'secondary'

type ButtonProps = PropsWithChildren<
  ButtonHTMLAttributes<HTMLButtonElement> & {
    variant?: ButtonVariant
    fullWidth?: boolean
  }
>

type ButtonClassOptions = {
  variant?: ButtonVariant
  fullWidth?: boolean
  className?: string
}

export const getButtonClassName = ({
  variant = 'primary',
  fullWidth = false,
  className,
}: ButtonClassOptions = {}) => {
  const baseClasses =
    'rounded-xl px-4 py-2 text-sm font-medium transition duration-150 hover:shadow-soft active:scale-[0.98] active:duration-90 disabled:cursor-not-allowed disabled:opacity-60'

  const variantClasses =
    variant === 'primary'
      ? 'bg-brand text-brand-foreground hover:bg-brand-strong'
      : 'border border-border bg-surface text-text-main hover:bg-surface-2'

  return `${baseClasses} ${variantClasses} ${fullWidth ? 'w-full' : ''} ${className ?? ''}`
}

export const Button = ({
  children,
  className,
  variant = 'primary',
  fullWidth = false,
  ...props
}: ButtonProps) => {
  return (
    <button
      className={getButtonClassName({ variant, fullWidth, className })}
      {...props}
    >
      {children}
    </button>
  )
}
