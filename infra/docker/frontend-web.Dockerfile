# syntax=docker/dockerfile:1

FROM node:22-bookworm-slim AS base
RUN corepack enable && corepack prepare pnpm@11.5.2 --activate
WORKDIR /app

# ---- deps ----
FROM base AS deps
COPY package.json pnpm-lock.yaml pnpm-workspace.yaml .npmrc ./
RUN pnpm install --frozen-lockfile

# ---- build ----
FROM base AS build
ARG NEXT_PUBLIC_API_URL
ENV NEXT_PUBLIC_API_URL=$NEXT_PUBLIC_API_URL
ENV NEXT_TELEMETRY_DISABLED=1
# next/font/google downloads fonts during build over HTTPS; needed in networks
# with SSL inspection (corporate proxy). Not propagated to the runner stage.
ENV NODE_TLS_REJECT_UNAUTHORIZED=0
COPY --from=deps /app/node_modules ./node_modules
COPY . .
# next build defaults to Turbopack in v16; --webpack avoids Linux binary issues
# mkdir -p ensures public/ exists even if the project has no public assets
RUN mkdir -p /app/public && pnpm exec next build --webpack

# ---- runner ----
FROM base AS runner
ENV NODE_ENV=production
ENV NEXT_TELEMETRY_DISABLED=1
ENV PORT=3001
ENV HOSTNAME=0.0.0.0
COPY --from=build /app/public ./public
COPY --from=build /app/.next/standalone ./
COPY --from=build /app/.next/static ./.next/static
EXPOSE 3001
HEALTHCHECK --interval=10s --timeout=5s --retries=5 --start-period=15s \
    CMD node -e "fetch('http://localhost:3001').then(r=>process.exit(r.ok?0:1)).catch(()=>process.exit(1))"
CMD ["node", "server.js"]
