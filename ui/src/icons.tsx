import {
  AudioLines, CircleHelp, Settings, Volume2, Cable, Mic, RefreshCw,
  Play, Square, ArrowLeft, X, ExternalLink, Check,
} from "lucide-react";
const icons = {
  logo: AudioLines, help: CircleHelp, settings: Settings, speaker: Volume2,
  cable: Cable, mic: Mic, refresh: RefreshCw, play: Play, stop: Square,
  back: ArrowLeft, close: X, external: ExternalLink, check: Check,
};
export function Icon({ name, size = 18 }: { name: keyof typeof icons; size?: number }) {
  const Component = icons[name];
  return <Component size={size} strokeWidth={1.8} aria-hidden="true" />;
}
