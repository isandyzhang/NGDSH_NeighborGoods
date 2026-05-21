export const FIRST_LISTING_SELLER_WELCOME_SEEN_KEY = 'ng.firstListingSellerWelcome.seen'

export const markFirstListingSellerWelcomeSeen = () => {
  if (typeof window === 'undefined') {
    return
  }
  window.localStorage.setItem(FIRST_LISTING_SELLER_WELCOME_SEEN_KEY, '1')
}

export const hasSeenFirstListingSellerWelcome = () => {
  if (typeof window === 'undefined') {
    return true
  }
  return window.localStorage.getItem(FIRST_LISTING_SELLER_WELCOME_SEEN_KEY) === '1'
}
