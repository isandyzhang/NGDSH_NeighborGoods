import type { InputHTMLAttributes } from 'react'

type InputProps = InputHTMLAttributes<HTMLInputElement> & {
  label: string
  error?: string
}

export const Input = ({ label, error, className, ...props }: InputProps) => {
  return (
    <label className="flex flex-col gap-2 text-sm text-text-subtle">
      <span>{label}</span>
      <input
        className={`w-full rounded-xl border border-border bg-surface px-3 py-2 text-text-main outline-none transition placeholder:text-text-muted focus:border-brand ${className ?? ''}`}
        {...props}
      />
      {error ? <span className="text-xs text-danger">{error}</span> : null}
    </label>
  )
}
