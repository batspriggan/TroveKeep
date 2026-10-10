import { get, post } from './client.js'

/**
 * First-run setup API. Both endpoints are anonymous on purpose: they must be reachable before any
 * account exists. The mutating one is gated by the transient setup token printed in the server
 * logs (`TroveKeep setup token: …`).
 *
 * `client.js` attaches the HTTP status to thrown errors, so callers can tell a bad payload (400),
 * a wrong token (403) and an already-configured instance (409) apart.
 */
export const getSetupStatus = () => get('/api/setup/status')

export const completeSetup = (payload) => post('/api/setup', payload)
