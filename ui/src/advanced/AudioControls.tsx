import { memo, useSyncExternalStore, useRef } from "react";
import { t } from "../i18n";
import { subscribeMeter, readMeter } from "../bridge";
import { gainToSlider, sliderToGain } from "./graph";
import { NumberInput } from "../fields";
const db = (n: number) => n.toFixed(1) + " dB";

export const NodeMeter = memo(function NodeMeter({ id, vertical = false, silent = false, detailed = false, label = t("SignalLevel") }: { id: string; vertical?: boolean; silent?: boolean; detailed?: boolean; label?: string }) {
  const meter = useSyncExternalStore(subscribeMeter, readMeter);
  const value = meter.nodes?.[id];
  const error = meter.errors?.[id];
  const peak = !silent ? value?.peak ?? 0 : 0;
  const level = Math.max(-60, Math.min(0, 20 * Math.log10(Math.max(.000001, peak))));
  const percent = peak > 0 ? (level + 60) / 60 * 100 : 0;
  const hold = useRef({ percent: 0, expires: 0, clippedUntil: 0 });
  const now = Date.now();
  if (silent) hold.current = { percent: 0, expires: 0, clippedUntil: 0 };
  else {
    if (percent >= hold.current.percent || now > hold.current.expires) { hold.current.percent = percent; hold.current.expires = now + 1500; }
    if (value?.clipped) hold.current.clippedUntil = now + 2000;
  }
  const clipped = now < hold.current.clippedUntil;
  const description = error || (peak <= 0 ? t("NoSignal") : level.toFixed(1) + " dBFS" + (clipped ? " · " + t("GraphClip") : ""));
  const bar = <div className={`node-meter ${vertical ? "vertical" : ""} ${clipped ? "clipped" : ""}`}
    role="meter" aria-label={label} aria-valuemin={-60} aria-valuemax={0} aria-valuenow={level} aria-valuetext={description}
    data-error={!!error}
    title={description}>
    <span style={{ [vertical ? "height" : "width"]: percent + "%" }} />
    {peak > 0 && <i className="meter-peak" style={{ [vertical ? "bottom" : "left"]: hold.current.percent + "%" }} />}
  </div>;
  return detailed ? <div className="meter-detail">{bar}<div className="meter-ticks" aria-hidden="true"><span>-60</span><span>-40</span><span>-20</span><span>0 dBFS</span></div><output aria-live="off">{description}</output></div> : bar;
});
export function MuteButton({ muted, onClick, label = "" }: { muted: boolean; onClick: () => void; label?: string }) {
  return <button className={`node-mute nodrag nopan ${muted ? "active" : ""}`} aria-label={(label ? label + " · " : "") + t("Mute")}
    title={t(muted ? "Unmute" : "Mute")} aria-pressed={muted} onClick={onClick}>M</button>;
}
export function Gain({ value, label, onChange, vertical = false, showRange = false }: { value: number; label: string; onChange: (n: number) => void; vertical?: boolean; showRange?: boolean }) {
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
    {showRange && <><span className="gain-min" aria-hidden="true">-60 dB</span><span className="gain-max" aria-hidden="true">+12 dB</span></>}
  </div>;
}
export function GainValue({ value, label, onChange }: { value: number; label: string; onChange: (n: number) => void }) {
  return <NumberInput className="gain-value nodrag nopan" value={value} label={label} min={-60} max={12} unit="dB" onChange={onChange} />;
}
