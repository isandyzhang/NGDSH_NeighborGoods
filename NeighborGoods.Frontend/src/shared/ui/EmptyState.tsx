type EmptyStateProps = {
  title: string
  description: string
}

export const EmptyState = ({ title, description }: EmptyStateProps) => {
  return (
    <div className="rounded-2xl border border-dashed border-border bg-surface p-10 text-center">
      <p className="text-lg font-semibold text-text-main">{title}</p>
      <p className="mt-2 text-sm text-text-subtle">{description}</p>
    </div>
  )
}
