import { AppRouter } from '@/app/AppRouter'
import { ApiWarmupGate } from '@/features/system/components/ApiWarmupGate'

function App() {
  return (
    <ApiWarmupGate>
      <AppRouter />
    </ApiWarmupGate>
  )
}

export default App
