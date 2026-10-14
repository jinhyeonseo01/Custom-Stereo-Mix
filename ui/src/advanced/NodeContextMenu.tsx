import { useLayoutEffect, useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { t } from "../i18n";
import { Power, Volume2, VolumeOff, Settings2, Trash, Check } from "../icons";
import type { AudioNodeConfig } from "../types";
import { nodeName } from "./graph";

export type NodeMenuAnchor = { nodeId: string; x: number; y: number; trigger: HTMLElement };
export function NodeContextMenu({ node, anchor, canEditRoute, change, inspect, remove, close }: {
  node: AudioNodeConfig; anchor: NodeMenuAnchor; canEditRoute: boolean;
  change: (patch: Partial<AudioNodeConfig>) => void; inspect: () => void; remove: () => void; close: () => void;
}) {
  const menu = useRef<HTMLDivElement>(null);
  const [position, setPosition] = useState({ x: anchor.x, y: anchor.y });
  function dismiss(restore = false) { close(); if (restore && anchor.trigger.isConnected) anchor.trigger.focus(); }
  useLayoutEffect(() => {
    const rect = menu.current!.getBoundingClientRect();
    setPosition({ x: Math.max(8, Math.min(anchor.x, innerWidth - rect.width - 8)), y: Math.max(8, Math.min(anchor.y, innerHeight - rect.height - 8)) });
    menu.current?.querySelector<HTMLButtonElement>('button:not(:disabled)')?.focus();
  }, [anchor]);
  useEffect(() => {
    const outside = (e: PointerEvent) => { if (!menu.current?.contains(e.target as Node) && !(anchor.trigger.matches("button") && anchor.trigger.contains(e.target as Node))) close(); };
    const moved = (e: Event) => { if (e.type === "scroll" && e.target instanceof Node && menu.current?.contains(e.target)) return; close(); };
    document.addEventListener("pointerdown", outside, true);
    window.addEventListener("resize", moved); window.addEventListener("scroll", moved, true);
    return () => { document.removeEventListener("pointerdown", outside, true); window.removeEventListener("resize", moved); window.removeEventListener("scroll", moved, true); };
  }, [anchor, close]);
  function run(action: () => void) { action(); dismiss(true); }
  return createPortal(<div ref={menu} className="node-context-menu" role="menu" aria-label={t("GraphNodeActions") + " · " + nodeName(node)} style={{ left: position.x, top: position.y }}
    onContextMenu={e => e.preventDefault()} onKeyDown={e => {
      e.stopPropagation();
      if (e.key === "Escape") { e.preventDefault(); dismiss(true); }
      if (e.key === "Tab") {
        e.preventDefault();
        const controls = Array.from(document.querySelectorAll<HTMLElement>('button:not(:disabled),input:not(:disabled),select:not(:disabled),textarea:not(:disabled),a[href],[tabindex="0"]'))
          .filter(el => !menu.current?.contains(el) && el.getClientRects().length && el.tabIndex >= 0);
        const index = controls.indexOf(anchor.trigger);
        const next = controls[index + (e.shiftKey ? -1 : 1)] ?? anchor.trigger;
        dismiss(); next.focus({ preventScroll: true });
      }
      if (["ArrowDown", "ArrowUp", "Home", "End"].includes(e.key)) {
        e.preventDefault(); const items = Array.from(menu.current!.querySelectorAll<HTMLButtonElement>('button:not(:disabled)'));
        const current = items.indexOf(document.activeElement as HTMLButtonElement);
        items[e.key === "Home" ? 0 : e.key === "End" ? items.length - 1 : (current + (e.key === "ArrowDown" ? 1 : -1) + items.length) % items.length]?.focus();
      }
    }}>
    <div className="node-menu-title">{nodeName(node)}</div>
    <button className="node-menu-activation" role="menuitemcheckbox" aria-checked={node.enabled} disabled={!canEditRoute}
      onClick={() => run(() => change({ enabled: !node.enabled }))}><Power size={19} /><span>{t("GraphActivation")}</span>{node.enabled && <Check size={16} />}</button>
    {!canEditRoute && <p className="node-menu-hint">{t("GraphEnableLocked")}</p>}
    <button role="menuitemcheckbox" aria-checked={node.muted} onClick={() => run(() => change({ muted: !node.muted }))}>
      {node.muted ? <VolumeOff size={19} /> : <Volume2 size={19} />}<span>{t("Mute")}</span>{node.muted && <Check size={16} />}</button>
    <div role="separator" />
    <button role="menuitem" onClick={() => run(inspect)}><Settings2 size={19} /><span>{t("Inspector")}</span></button>
    <div role="separator" />
    <button className="node-menu-danger" role="menuitem" disabled={!canEditRoute} onClick={() => run(remove)}><Trash size={19} /><span>{t("NodeDelete")}</span></button>
  </div>, document.body);
}
