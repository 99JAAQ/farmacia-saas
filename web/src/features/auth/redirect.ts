/**
 * Where to go after logging in. Only internal paths are accepted: otherwise a crafted link could send a user
 * who just logged in to another site (open redirect), e.g. a phishing copy of this one.
 */
export function safeRedirectPath(candidate: unknown): string {
  if (typeof candidate !== 'string') return '/'

  // "/x" is internal; "//evil.com" and "/\evil.com" are read by browsers as another host.
  const isInternal = candidate.startsWith('/') && !candidate.startsWith('//') && !candidate.startsWith('/\\')
  return isInternal && !candidate.startsWith('/login') ? candidate : '/'
}
