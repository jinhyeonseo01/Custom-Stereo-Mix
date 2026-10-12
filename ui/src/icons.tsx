import "./material-icons.css";
const symbol = (name: string) => function MaterialSymbol({ size = 18 }: { size?: number; strokeWidth?: number }) {
  return <span className="material-icon" style={{ fontSize: size, width: size, height: size }} aria-hidden="true">{name}</span>;
};
export const AudioLines = symbol("graphic_eq"), CircleHelp = symbol("help"), Settings = symbol("settings"),
  Volume2 = symbol("volume_up"), Cable = symbol("cable"), Mic = symbol("mic"), RefreshCw = symbol("refresh"),
  Play = symbol("play_arrow"), Square = symbol("stop"), ArrowLeft = symbol("arrow_back"), X = symbol("close"),
  ExternalLink = symbol("open_in_new"), Check = symbol("check"), SlidersHorizontal = symbol("tune"),
  Headphones = symbol("headphones"), Music2 = symbol("music_note"), Monitor = symbol("web_asset"),
  Plus = symbol("add"), Maximize2 = symbol("zoom_out_map"), PanelLeft = symbol("space_dashboard"),
  PanelBottom = symbol("width_full"), MoreHorizontal = symbol("more_horiz"), Undo2 = symbol("undo"),
  Redo2 = symbol("redo"), ArrowDownToLine = symbol("download"), ArrowUpFromLine = symbol("upload"),
  Settings2 = symbol("settings"), LayoutGrid = symbol("grid_view"), ZoomIn = symbol("zoom_in"), ZoomOut = symbol("zoom_out");
const icons = {
  logo: AudioLines, help: CircleHelp, settings: Settings, speaker: Volume2,
  cable: Cable, mic: Mic, refresh: RefreshCw, play: Play, stop: Square,
  back: ArrowLeft, close: X, external: ExternalLink, check: Check,
};
export function Icon({ name, size = 18 }: { name: keyof typeof icons; size?: number }) {
  const Component = icons[name];
  return <Component size={size} strokeWidth={1.8} aria-hidden="true" />;
}
