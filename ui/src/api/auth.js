import { get } from './client.js'

/**
 * Which login methods the backend offers. Anonymous endpoint: safe to call from the login page
 * before any session exists. `local` is always true; `oidc` mirrors `Auth:Oidc:Enabled`.
 */
export const getProviders = () => get('/api/auth/providers')
