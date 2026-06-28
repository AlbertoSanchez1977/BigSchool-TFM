import { AI_SCANNER_ENABLED } from '@/lib/config/aiScanner'
import AiScannerView from '@/components/ai-scanner/ai-scanner-view'

export default function AiScannerPage() {
  return <AiScannerView enabled={AI_SCANNER_ENABLED} />
}
