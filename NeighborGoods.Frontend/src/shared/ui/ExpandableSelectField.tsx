import { useEffect, useMemo, useRef, useState } from 'react'
import type { LookupItem } from '@/features/lookups/api/lookupApi'

const invalidBorderClass = 'border-2 border-[#dc2626] transition-colors duration-300 ease-out'

type ExpandableSelectFieldProps = {
  label: string
  value: number
  options: LookupItem[]
  onChange: (nextValue: number) => void
  invalid?: boolean
  placeholder?: string
  includeEmptyOption?: boolean
}

export const ExpandableSelectField = ({
  label,
  value,
  options,
  onChange,
  invalid = false,
  placeholder = '-',
  includeEmptyOption = false,
}: ExpandableSelectFieldProps) => {
  const [open, setOpen] = useState(false)
  const containerRef = useRef<HTMLDivElement | null>(null)

  const selectableOptions = useMemo(
    () => (includeEmptyOption ? options.filter((item) => item.id > 0) : options),
    [includeEmptyOption, options],
  )

  const selectedLabel = useMemo(() => {
    if (includeEmptyOption && value <= 0) {
      return placeholder
    }
    return selectableOptions.find((item) => item.id === value)?.displayName ?? placeholder
  }, [includeEmptyOption, placeholder, selectableOptions, value])

  useEffect(() => {
    if (!open) {
      return
    }

    const handleOutsideClick = (event: MouseEvent) => {
      if (!containerRef.current?.contains(event.target as Node)) {
        setOpen(false)
      }
    }

    const handleEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setOpen(false)
      }
    }

    document.addEventListener('mousedown', handleOutsideClick)
    document.addEventListener('keydown', handleEscape)
    return () => {
      document.removeEventListener('mousedown', handleOutsideClick)
      document.removeEventListener('keydown', handleEscape)
    }
  }, [open])

  const handleSelect = (nextValue: number) => {
    onChange(nextValue)
    setOpen(false)
  }

  return (
    <div ref={containerRef} className="relative flex flex-col gap-2 text-lg text-text-subtle">
      <span className="text-[1.45rem] font-bold leading-tight text-text-main">{label}</span>
      <button
        type="button"
        onClick={() => setOpen((current) => !current)}
        className={`inline-flex w-full items-center justify-between rounded-xl border bg-surface px-3 py-3 text-left text-xl text-text-main outline-none transition hover:bg-surface-2 focus:border-brand ${
          invalid ? invalidBorderClass : 'border-border'
        }`}
        aria-expanded={open}
        aria-haspopup="listbox"
      >
        <span>{selectedLabel}</span>
        <svg
          viewBox="0 0 20 20"
          className={`h-5 w-5 shrink-0 transition-transform ${open ? 'rotate-180' : ''}`}
          fill="none"
          stroke="currentColor"
          strokeWidth="2"
          aria-hidden="true"
        >
          <path d="M5 7l5 6 5-6" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
      </button>
      <div
        className={`absolute left-0 right-0 top-full z-30 mt-2 overflow-hidden rounded-xl border border-border bg-surface shadow-lg transition-[max-height,opacity] duration-220 ease-out ${
          open ? 'max-h-64 opacity-100' : 'pointer-events-none max-h-0 border-transparent opacity-0'
        }`}
      >
        <div className="max-h-64 overflow-y-auto py-1">
          {includeEmptyOption ? (
            <button
              type="button"
              role="option"
              aria-selected={value <= 0}
              onClick={() => handleSelect(0)}
              className={`w-full px-3 py-2 text-left text-xl transition ${
                value <= 0
                  ? 'bg-[#D6B897] font-semibold text-text-main'
                  : 'text-text-main hover:bg-surface-2'
              }`}
            >
              {placeholder}
            </button>
          ) : null}
          {selectableOptions.map((item) => {
            const selected = item.id === value
            return (
              <button
                key={item.id}
                type="button"
                role="option"
                aria-selected={selected}
                onClick={() => handleSelect(item.id)}
                className={`w-full px-3 py-2 text-left text-xl transition ${
                  selected
                    ? 'bg-[#D6B897] font-semibold text-text-main'
                    : 'text-text-main hover:bg-surface-2'
                }`}
              >
                {item.displayName}
              </button>
            )
          })}
        </div>
      </div>
    </div>
  )
}
