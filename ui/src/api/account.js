import { post } from './client.js'

// Self-service account endpoint. The server reports a wrong current password as
// 400 `{ error: "Current password is incorrect." }`, which `client.js` surfaces as the message.
export const changePassword = (currentPassword, newPassword) =>
  post('/api/auth/change-password', { currentPassword, newPassword })
