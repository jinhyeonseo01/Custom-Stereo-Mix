import { useEffect, useId, useRef, useState } from "react";
import { t } from "./i18n";

export function NumberInput({ value, label, min, max, step = .1, unit = "", disabled = false, showLabel = false, className = "", onChange }:
  { value: number; label: string; min: number; max: number; step?: number; unit?: string; disabled?: boolean; showLabel?: boolean; className?: string;
    onChange: (value: number) => void | boolean | Promise<boolean> }) {
  const format = (n: number) => step >= 1 ? String(n) : n.toFixed(1);
  const [draft, setDraft] = useState(format(value)), [error, setError] = useState("");
  const editing = useRef(false), cancelled = useRef(false), id = useId();
  const range = t("NumberRange", min, max, unit);
  useEffect(() => { if (!editing.current) { setDraft(format(value)); setError(""); } }, [value, step]);
  async function commit() {
    editing.current = false;
    if (cancelled.current) { cancelled.current = false; return; }
    const n = Number(draft), valid = draft.trim() && Number.isFinite(n) && n >= min && n <= max && (step < 1 || Number.isInteger(n));
    if (!valid) { setError(range); return; }
    const next = step < 1 ? Math.round(n * 10) / 10 : n;
    setError(""); setDraft(format(next));
    if (next !== value && await onChange(next) === false) { setDraft(format(value)); setError(t("RequestFailed")); }
  }
  return <div className={`number-field ${className}`}>
    {showLabel && <label htmlFor={id}>{label}</label>}
    <div className="number-input-row"><input id={id} type="number" min={min} max={max} step={step} value={draft} disabled={disabled}
      aria-label={label} aria-invalid={!!error} aria-describedby={id + "-hint"} title={range}
      onFocus={() => { editing.current = true; }} onChange={e => setDraft(e.target.value)} onBlur={() => void commit()}
      onKeyDown={e => {
        if (e.key === "Enter") { e.preventDefault(); e.currentTarget.blur(); }
        if (e.key === "Escape") {
          e.preventDefault(); e.stopPropagation(); cancelled.current = true; editing.current = false;
          setDraft(format(value)); setError(""); e.currentTarget.blur();
        }
      }} />{unit && <span className="number-unit">{unit}</span>}</div>
    <small id={id + "-hint"} className={error ? "field-error" : "sr-only"} role={error ? "alert" : undefined}>{error || range}</small>
  </div>;
}
export function TextInput({ value, onChange, ...props }: { value: string; onChange: (value: string) => void } & Omit<React.InputHTMLAttributes<HTMLInputElement>, "value" | "onChange">) {
  const [draft, setDraft] = useState(value); const editing = useRef(false), cancelled = useRef(false);
  useEffect(() => { if (!editing.current) setDraft(value); }, [value]);
  return <input {...props} value={draft} onFocus={() => { editing.current = true; }} onChange={e => setDraft(e.target.value)}
    onBlur={() => { editing.current = false; if (cancelled.current) cancelled.current = false; else if (draft !== value) onChange(draft); }}
    onKeyDown={e => {
      if (e.key === "Enter") { e.preventDefault(); e.currentTarget.blur(); }
      if (e.key === "Escape") { e.preventDefault(); e.stopPropagation(); cancelled.current = true; setDraft(value); e.currentTarget.blur(); }
    }} />;
}
