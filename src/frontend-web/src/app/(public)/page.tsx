import { NavbarPublic } from "@/components/navbar-public"
import { Footer } from "@/components/footer"
import { Hero } from "@/components/sections/hero"
import { Expenses } from "@/components/sections/expenses"
import { Investments } from "@/components/sections/investments"
import { AiScanner } from "@/components/sections/ai-scanner"
import { Cta } from "@/components/sections/cta"

export default function Home() {
  return (
    <div className="flex min-h-screen flex-col bg-background font-sans">
      <NavbarPublic />
      <main className="flex-1">
        <Hero />
        <Expenses />
        <Investments />
        <AiScanner />
        <Cta />
      </main>
      <Footer />
    </div>
  )
}
