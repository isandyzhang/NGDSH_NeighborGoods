import { AppRouter } from '@/app/AppRouter'
import { ListingExpiryLoginPrompt } from '@/features/listings/components/ListingExpiryLoginPrompt'
import { ApiWarmupGate } from '@/features/system/components/ApiWarmupGate'

function App() {
  return (
    <ApiWarmupGate>
      <AppRouter />
      <ListingExpiryLoginPrompt />
    </ApiWarmupGate>
  )
}

export default App
