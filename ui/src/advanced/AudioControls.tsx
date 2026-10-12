import { memo, useSyncExternalStore, useState, useEffect, useRef } from "react";
import { t } from "../i18n";
import { subscribeMeter, readMeter } from "../bridge";
import { gainToSlider, sliderToGain } from "./graph";
const db = (n: number) => n.toFixed(1) + " dB";

export const NodeMeter = memo(function NodeMeter({ id, vertical = false, silent = false }: { id: string; vertical?: boolean; silent?: boolean }) {
  const meter = useSyncExternalStore(subscribeMeter, readMeter);
  const value = meter.nodes?.[id];
  const error = meter.errors?.[id];
  const percent = value && !silent ? Math.min(100, Math.max(0, 100 + 25 * Math.log10(Math.max(.0001, value.peak)))) : 0;
  return <div className={`node-meter ${vertical ? "vertical" : ""} ${value?.clipped ? "clipped" : ""}`}
    role="meter" aria-label={t("SignalLevel")} aria-valuemin={0} aria-valuemax={100} aria-valuenow={Math.round(percent)}
    data-error={!!error}
    title={error || (silent ? t("NoSignal") : value?.clipped ? t("GraphClip") : value ? db(20 * Math.log10(Math.max(.0001, value.peak))) : t("NoSignal"))}>
    <span style={{ [vertical ? "height" : "width"]: percent + "%" }} />
  </div>;
});
export function MuteButton({ muted, onClick }: { muted: boolean; onClick: () => void }) {
  return <button className={`node-mute nodrag nopan ${muted ? "active" : ""}`} aria-label={t("Mute")}
    title={t(muted ? "Unmute" : "Mute")} aria-pressed={muted} onClick={onClick}>M</button>;
}
export function Gain({ value, label, onChange, vertical = false }: { value: number; label: string; onChange: (n: number) => void; vertical?: boolean }) {
  return <div className={`gain-control ${vertical ? "vertical-gain" : ""}`}>
    <input className={`graph-gain nodrag nopan ${vertical ? "vertical-fader" : ""}`} type="range"
      min={-100} max={100} step={1} value={gainToSlider(value)} aria-label={label} aria-valuetext={db(value)}
      aria-valuemin={-60} aria-valuemax={12} aria-valuenow={value} title={t("GraphGainReset")}
      onDoubleClick={() => onChange(0)}
      onKeyDown={e => {
        if (["ArrowLeft", "ArrowDown", "ArrowRight", "ArrowUp", "Home", "End", "0"].includes(e.key)) {
          e.preventDefault();
          const next = e.key === "Home" ? -60 : e.key === "End" ? 12 : e.key === "0" ? 0 :
            value + (e.key === "ArrowLeft" || e.key === "ArrowDown" ? -1 : 1) * (e.shiftKey ? 1 : .1);
          onChange(Math.max(-60, Math.min(12, Math.round(next * 10) / 10)));
        }
      }}
      onChange={e => onChange(sliderToGain(Number(e.target.value)))}
      style={{ "--gain-fill": ((gainToSlider(value) + 100) / 2) + "%" } as React.CSSProperties} />
    <span className="gain-zero" aria-hidden="true">0</span>
  </div>;
}
export function GainValue({ value, label, onChange }: { value: number; label: string; onChange: (n: number) => void }) {
  const [draft, setDraft] = useState(value.toFixed(1));
  const cancelled = useRef(false);
  useEffect(() => setDraft(value.toFixed(1)), [value]);
  const commitValue = () => {
    if (cancelled.current) { cancelled.current = false; return; }
    const n = Number(draft);
    if (draft.trim() && Number.isFinite(n) && n >= -60 && n <= 12) onChange(Math.round(n * 10) / 10);
    else setDraft(value.toFixed(1));
  };
  return <label className="gain-value nodrag nopan"><input type="number" min={-60} max={12} step={.1}
    aria-label={label} value={draft} onChange={e => setDraft(e.target.value)} onBlur={commitValue}
    onKeyDown={e => { if (e.key === "Enter") e.currentTarget.blur(); if (e.key === "Escape") {
      e.preventDefault(); e.stopPropagation(); cancelled.current = true;
      setDraft(value.toFixed(1)); e.currentTarget.blur();
    } }} /><span>dB</span></label>;
}
