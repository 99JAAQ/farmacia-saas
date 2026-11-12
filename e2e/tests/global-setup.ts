import { admin, call, login } from './api'

/**
 * A fresh deployment has one platform administrator with a temporary password (ProductionBootstrap). Change it once,
 * for every test. Safe to run again: if the password was already changed, there is nothing to do.
 */
export default async function globalSetup() {
  const ready = await login(admin.email, admin.password).catch(() => null)
  if (ready) return

  const session = await login(admin.email, admin.bootstrapPassword)
  await call('POST', '/api/auth/change-password', session.accessToken, {
    currentPassword: admin.bootstrapPassword,
    newPassword: admin.password,
  })
}
