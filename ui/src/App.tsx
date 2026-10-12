import React, {
  useEffect,
  useRef,
  useState,
  useSyncExternalStore,
} from "react";

import {
  connect,
  request,
  subscribeState,
  readState,
  subscribeMeter,
  readMeter,
} from "./bridge";
import { Icon } from "./icons";
import type { AppState, Command, Device, Patch, Settings } from "./types";

const db = (n: number) => `${n > 0 ? "+" : ""}${n.toFixed(1)} dB`;
export default function App() {
  const state = useSyncExternalStore(subscribeState, readState);
  const [error, setError] = useState("");
  const [connecting, setConnecting] = useState(true);
  useEffect(() => {
    connect()
      .catch((e) => setError(e.message))
      .finally(() => setConnecting(false));
  }, []);
  async function act(command: Command, payload?: unknown) {
    setError("");
    try {
      await request(command, payload);
      return true;
    } catch (e) {
      setError(e instanceof Error ? e.message : "요청에 실패했습니다.");
      return false;
    }
  }
  const patch = (value: Patch) => act("patch", value);
  if (!state)
    return (
      <main className="loading">
        <div className="brand-mark">
          <Icon name="logo" size={26} />
        </div>
        <h1>Custom Stereo Mix</h1>
        <p role="status">
          {error ||
            (connecting
              ? "오디오 장치와 화면을 연결하고 있습니다…"
              : "연결을 기다리고 있습니다…")}
        </p>
      </main>
    );
  return (
    <div className="app-shell">
      <header className="app-header">
        <div className="brand">
          <div className="brand-mark">
            <Icon name="logo" size={25} />
          </div>
          <div className="brand-copy">
            <h1>Custom Stereo Mix</h1>
            <p>PC의 소리를 원하는 앱으로</p>
          </div>
        </div>
        <nav className="header-actions" aria-label="프로그램 메뉴">
          <span
            className={`status-chip ${state.isRouting ? "running" : ""}`}
            title={state.statusText}
          >
            <span />
            {state.statusText}
          </span>
          <button
            className={`button subtle ${state.view === "help" ? "selected" : ""}`}
            onClick={() =>
              act(state.view === "help" ? "closeView" : "openHelp")
            }
            title="스테레오 믹스 사용을 위한 추가 설정"
            aria-pressed={state.view === "help"}
          >
            <Icon name="help" />
            <span>사용법</span>
          </button>
          <button
            className={`button subtle ${state.view === "settings" ? "selected" : ""}`}
            onClick={() =>
              act(state.view === "settings" ? "closeView" : "openSettings")
            }
            aria-pressed={state.view === "settings"}
          >
            <Icon name="settings" />
            <span>설정</span>
          </button>
        </nav>
      </header>
      {window.__stereoPreview && (
        <div className="preview-note">
          브라우저 미리보기 · 실제 오디오 연결 없음
        </div>
      )}
      <main className="page-scroll">
        <div className="page-content">
          {(error || state.noticeText) && (
            <div className="notice" role="alert">
              <span>{error || state.noticeText}</span>
              {error && (
                <button
                  aria-label="오류 알림 닫기"
                  className="icon-button"
                  onClick={() => setError("")}
                >
                  <Icon name="close" size={16} />
                </button>
              )}
            </div>
          )}
          {state.view === "main" && (
            <MainPage state={state} act={act} patch={patch} />
          )}
          {state.view === "settings" && (
            <SettingsPage state={state} act={act} patch={patch} />
          )}
          {state.view === "help" && <HelpPage />}
        </div>
      </main>
      <footer className="app-footer">
        {state.view === "help" ? (
          <>
            <label className="check-label">
              <input
                type="checkbox"
                checked={state.guideDraft}
                onChange={(e) =>
                  act("guideDraft", { dontShowAgain: e.target.checked })
                }
              />
              다시 보지 않기
            </label>
            <button className="button" onClick={() => act("closeView")}>
              닫기
            </button>
          </>
        ) : (
          <>
            <div className="footer-copy">
              <strong>{state.engineLine}</strong>
              <span>{state.trayHint}</span>
            </div>
            {state.view === "settings" ? (
              <button className="button" onClick={() => act("closeView")}>
                <Icon name="back" size={16} />
                메인으로
              </button>
            ) : (
              <button
                className={`button route-action ${state.isRouting ? "danger" : "primary"}`}
                disabled={
                  state.busy ||
                  (state.isRouting
                    ? !state.isStopEnabled
                    : !state.isStartEnabled)
                }
                onClick={() => act(state.isRouting ? "stop" : "start")}
              >
                <Icon name={state.isRouting ? "stop" : "play"} size={17} />
                {state.busy
                  ? "처리 중…"
                  : state.isRouting
                    ? "연결 중지"
                    : "소리 연결 시작"}
              </button>
            )}
          </>
        )}
      </footer>
      <UpdateDialog state={state} act={act} />
    </div>
  );
}

type Actions = {
  state: AppState;
  act: (c: Command, p?: unknown) => Promise<boolean>;
  patch: (p: Patch) => Promise<boolean>;
};
function UpdateDialog({ state, act }: Pick<Actions, "state" | "act">) {
  const dialog = useRef<HTMLDialogElement>(null);
  const [opening, setOpening] = useState(false);
  const [error, setError] = useState("");
  useEffect(() => {
    setError("");
    if (state.isUpdateDialogOpen) dialog.current?.showModal();
    else dialog.current?.close();
  }, [state.isUpdateDialogOpen]);
  async function openInstaller() {
    setOpening(true);
    const ok = await act("downloadUpdate");
    setOpening(false);
    if (ok) await act("dismissUpdate");
    else
      setError("설치기 링크를 열지 못했습니다. 설정에서 다시 확인해 주세요.");
  }
  return (
    <dialog
      ref={dialog}
      aria-labelledby="update-dialog-title"
      onCancel={(event) => {
        event.preventDefault();
        void act("dismissUpdate");
      }}
    >
      <div className="dialog-body">
        <h3 id="update-dialog-title">새 업데이트가 있습니다</h3>
        <p>
          {state.appVersion} → {state.latestVersion}
        </p>
        <p>
          새 버전의 Windows 설치기를 받을 수 있습니다. 다운로드 후 트레이에서
          앱을 종료하고 설치기를 실행해 주세요.
        </p>
        {state.hasReleasePage && (
          <button className="button small" onClick={() => act("openRelease")}>
            <Icon name="external" size={14} />
            변경 사항 보기
          </button>
        )}
        {error && <p role="alert">{error}</p>}
        <div className="dialog-actions">
          <button className="button" onClick={() => act("dismissUpdate")}>
            나중에
          </button>
          <button
            className="button primary"
            disabled={!state.hasUpdateInstaller || opening}
            onClick={openInstaller}
          >
            {opening ? "여는 중…" : "설치기 다운로드"}
          </button>
        </div>
      </div>
    </dialog>
  );
}
function DeviceSelect({
  label,
  icon,
  devices,
  selected,
  disabled,
  onChange,
}: {
  label: string;
  icon: "speaker" | "cable" | "mic";
  devices: Device[];
  selected: string | null;
  disabled: boolean;
  onChange: (id: string) => void;
}) {
  const current = devices.find((d) => d.id === selected);
  return (
    <label className="device-field">
      <span className="field-title">
        <Icon name={icon} size={16} />
        {label}
      </span>
      <select
        value={selected ?? ""}
        disabled={disabled}
        onChange={(e) => onChange(e.target.value)}
        title={current?.name}
      >
        <option value="" disabled>
          {devices.length ? "장치 선택" : "사용 가능한 장치 없음"}
        </option>
        {devices.map((d) => (
          <option key={d.id} value={d.id}>
            {d.name}
          </option>
        ))}
      </select>
      <span className="caption">
        {current?.summary ?? "장치를 연결한 뒤 새로고침해 주세요"}
      </span>
    </label>
  );
}
function MainPage({ state, act, patch }: Actions) {
  return (
    <>
      <div className="section-heading">
        <h2>소리 연결</h2>
        <button
          className="button small"
          disabled={!state.canEditRoute}
          onClick={() => act("refresh")}
        >
          <Icon name="refresh" size={14} />
          새로고침
        </button>
      </div>
      <div className="route-grid">
        <section className="card">
          <DeviceSelect
            label="재생 소리"
            icon="speaker"
            devices={state.sources}
            selected={state.sourceDeviceId}
            disabled={!state.canEditRoute}
            onChange={(sourceDeviceId) => patch({ sourceDeviceId })}
          />
        </section>
        <span className="route-arrow" aria-hidden="true">
          →
        </span>
        <section className="card">
          <DeviceSelect
            label="보낼 장치"
            icon="cable"
            devices={state.targets}
            selected={state.targetDeviceId}
            disabled={!state.canEditRoute}
            onChange={(targetDeviceId) => patch({ targetDeviceId })}
          />
        </section>
      </div>
      <p className="route-help caption">{state.targetMessage}</p>
      <SignalMonitor />
      <div className="gain-row">
        <span className="field-title">출력 볼륨</span>
        <Range
          label="출력 볼륨"
          value={state.settings.gainDb}
          min={-12}
          max={12}
          disabled={!state.canEditSettings}
          onChange={(gainDb) => patch({ gainDb })}
        />
        <output>{db(state.settings.gainDb)}</output>
      </div>
      <section className="card mic-card">
        <Switch
          label="마이크도 함께 보내기"
          icon="mic"
          value={state.settings.isMicMixEnabled}
          disabled={!state.canEditRoute}
          onChange={(isMicMixEnabled) => patch({ isMicMixEnabled })}
        />
        {state.settings.isMicMixEnabled && (
          <div className="mic-details">
            <DeviceSelect
              label="마이크"
              icon="mic"
              devices={state.microphones}
              selected={state.microphoneDeviceId}
              disabled={!state.canEditRoute}
              onChange={(microphoneDeviceId) => patch({ microphoneDeviceId })}
            />
            <div className="gain-row">
              <Range
                label="마이크 볼륨"
                value={state.settings.micGainDb}
                min={-12}
                max={12}
                disabled={!state.canEditSettings}
                onChange={(micGainDb) => patch({ micGainDb })}
              />
              <output>{db(state.settings.micGainDb)}</output>
            </div>
          </div>
        )}
      </section>
    </>
  );
}
function SignalMonitor() {
  const meter = useSyncExternalStore(subscribeMeter, readMeter);
  const values = meter.samples.length ? meter.samples : Array(96).fill(0);
  return (
    <section className="card signal-card" aria-label="출력 신호 모니터">
      <div className="signal-heading">
        <div>
          <strong>신호 모니터</strong>
          <span className="caption">{meter.signalState}</span>
        </div>
        <output className="level" aria-label="신호 레벨">
          {Math.round(meter.levelPercent)}%
        </output>
      </div>
      <svg
        viewBox="0 0 640 64"
        preserveAspectRatio="none"
        className="waveform"
        aria-hidden="true"
      >
        <defs>
          <linearGradient id="wave-blue" x1="0" y1="1" x2="0" y2="0">
            <stop stopColor="#ADC7FF" />
            <stop offset="1" stopColor="#416FF2" />
          </linearGradient>
        </defs>
        {[16, 32, 48].map((y) => (
          <line key={y} x1="0" x2="640" y1={y} y2={y} stroke="#EDF1F8" />
        ))}
        {values.map((n, i) => {
          const h = Math.max(2, Math.min(1, n) * 60);
          return (
            <rect
              key={i}
              x={(i * 640) / values.length}
              y={64 - h}
              width={Math.max(1, 640 / values.length - 2)}
              height={h}
              rx="1"
              fill="url(#wave-blue)"
            />
          );
        })}
      </svg>
      <div className="level-track">
        <span style={{ width: `${Math.min(100, meter.levelPercent)}%` }} />
      </div>
    </section>
  );
}
function Switch({
  label,
  description,
  icon,
  value,
  disabled,
  onChange,
}: {
  label: string;
  description?: string;
  icon?: "mic";
  value: boolean;
  disabled?: boolean;
  onChange: (value: boolean) => void;
}) {
  return (
    <label className="switch-row">
      <span className="switch-copy">
        <span className="field-title">
          {icon && <Icon name={icon} size={16} />}
          {label}
        </span>
        {description && <span className="caption">{description}</span>}
      </span>
      <input
        type="checkbox"
        role="switch"
        className="switch-input"
        checked={value}
        disabled={disabled}
        onChange={(e) => onChange(e.target.checked)}
      />
      <span className="switch-visual" aria-hidden="true" />
    </label>
  );
}
function Range({
  label,
  value,
  min,
  max,
  step = 0.1,
  disabled,
  onChange,
}: {
  label: string;
  value: number;
  min: number;
  max: number;
  step?: number;
  disabled?: boolean;
  onChange: (value: number) => void;
}) {
  const [draft, setDraft] = useState(value);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const dragging = useRef(false);
  useEffect(() => {
    if (!dragging.current) setDraft(value);
  }, [value]);
  useEffect(
    () => () => {
      if (timer.current) clearTimeout(timer.current);
    },
    [],
  );
  function commit(n: number) {
    if (timer.current) clearTimeout(timer.current);
    timer.current = null;
    onChange(n);
  }
  return (
    <input
      type="range"
      aria-label={label}
      value={draft}
      min={min}
      max={max}
      step={step}
      disabled={disabled}
      onPointerDown={() => {
        dragging.current = true;
      }}
      onChange={(e) => {
        const n = Number(e.target.value);
        setDraft(n);
        if (timer.current) clearTimeout(timer.current);
        timer.current = setTimeout(() => onChange(n), 80);
      }}
      onPointerUp={(e) => {
        dragging.current = false;
        commit(Number(e.currentTarget.value));
      }}
      onBlur={(e) => {
        dragging.current = false;
        commit(Number(e.currentTarget.value));
      }}
      style={
        {
          "--range-fill": `${((draft - min) / (max - min)) * 100}%`,
        } as React.CSSProperties
      }
    />
  );
}
function Numeric({
  label,
  name,
  state,
  min,
  max,
  step,
  patch,
}: {
  label: string;
  name: keyof Settings;
  state: AppState;
  min: number;
  max: number;
  step: number;
  patch: Actions["patch"];
}) {
  const current = Number(state.settings[name]);
  const [draft, setDraft] = useState(String(current));
  useEffect(() => setDraft(String(current)), [current]);
  async function commit(input: HTMLInputElement) {
    if (!input.checkValidity() || draft.trim() === "") {
      input.reportValidity();
      setDraft(String(current));
      return;
    }
    if (!(await patch({ [name]: Number(draft) }))) setDraft(String(current));
  }
  return (
    <label className="numeric-field">
      <span className="caption">{label}</span>
      <input
        type="number"
        value={draft}
        min={min}
        max={max}
        step={step}
        disabled={!state.canEditRoute}
        onChange={(e) => setDraft(e.target.value)}
        onBlur={(e) => commit(e.currentTarget)}
        onKeyDown={(e) => {
          if (e.key === "Enter") e.currentTarget.blur();
        }}
      />
    </label>
  );
}
function SettingsPage({ state, act, patch }: Actions) {
  const [resetOpen, setResetOpen] = useState(false);
  const dialog = useRef<HTMLDialogElement>(null);
  useEffect(() => {
    if (resetOpen) dialog.current?.showModal();
    else dialog.current?.close();
  }, [resetOpen]);
  return (
    <>
      <div className="page-heading">
        <h2>설정</h2>
        <p className="caption">변경한 설정은 자동으로 저장됩니다</p>
      </div>
      <section className="card settings-card">
        <div className="section-heading">
          <div>
            <h3>업데이트</h3>
            <p className="caption">{state.appVersion}</p>
          </div>
          <button
            className="button"
            disabled={state.updating}
            onClick={() => act("checkUpdates")}
          >
            {state.updating ? "확인 중…" : "업데이트 확인"}
          </button>
        </div>
        <p className="caption" role="status">
          {state.updateMessage}
        </p>
        {state.hasReleasePage && (
          <button className="button small" onClick={() => act("openRelease")}>
            <Icon name="external" size={14} />
            릴리즈 페이지
          </button>
        )}
      </section>
      <section className="card settings-card">
        <h3>프로그램 동작</h3>
        <Switch
          label="시스템 트레이 사용"
          description="닫기와 최소화 시 트레이에서 계속 실행"
          value={state.settings.minimizeToTray}
          disabled={!state.canEditSettings}
          onChange={(minimizeToTray) => patch({ minimizeToTray })}
        />
        <p className="inset-note caption">
          트레이 우클릭 → 종료는 항상 프로그램을 종료합니다.
          <br />
          트레이 사용을 끄면 닫기 버튼으로도 종료됩니다.
        </p>
      </section>
      <section className="card settings-card">
        <h3>소리 보정</h3>
        <Switch
          label="큰 소리 부드럽게 줄이기"
          description="컴프레서 · 순간적인 큰 소리를 줄입니다"
          value={state.settings.compressorEnabled}
          disabled={!state.canEditSettings}
          onChange={(compressorEnabled) => patch({ compressorEnabled })}
        />
        {state.settings.compressorEnabled && (
          <div className="two-fields">
            <label className="slider-field">
              <span className="caption">
                기준 {state.settings.compressorThresholdDb.toFixed(1)} dB
              </span>
              <Range
                label="컴프레서 기준"
                value={state.settings.compressorThresholdDb}
                min={-40}
                max={-3}
                disabled={!state.canEditSettings}
                onChange={(compressorThresholdDb) =>
                  patch({ compressorThresholdDb })
                }
              />
            </label>
            <label className="slider-field">
              <span className="caption">
                압축 비율 {state.settings.compressorRatio.toFixed(1)}:1
              </span>
              <Range
                label="컴프레서 압축 비율"
                value={state.settings.compressorRatio}
                min={1}
                max={10}
                disabled={!state.canEditSettings}
                onChange={(compressorRatio) => patch({ compressorRatio })}
              />
            </label>
          </div>
        )}
      </section>
      <section className="card settings-card">
        <h3>오디오 엔진</h3>
        <div className="two-fields">
          <Numeric
            label="샘플레이트 (Hz)"
            name="sampleRate"
            state={state}
            min={22050}
            max={192000}
            step={1}
            patch={patch}
          />
          <Numeric
            label="채널 · 1 모노 / 2 스테레오"
            name="channels"
            state={state}
            min={1}
            max={2}
            step={1}
            patch={patch}
          />
          <Numeric
            label="지연 (ms)"
            name="latencyMilliseconds"
            state={state}
            min={10}
            max={250}
            step={1}
            patch={patch}
          />
          <Numeric
            label="버퍼 용량 (ms)"
            name="bufferMilliseconds"
            state={state}
            min={80}
            max={1000}
            step={1}
            patch={patch}
          />
        </div>
        <p className="caption">
          기본값: 48 kHz · 스테레오 · 지연 50 ms
          <br />
          소리 연결을 중지한 뒤 변경할 수 있습니다.
        </p>
      </section>
      <section className="card settings-card">
        <h3>설정 관리</h3>
        <p className="caption">
          장치 선택, 볼륨, 보정, 엔진, 트레이 설정을 기본값으로 되돌립니다. 실행
          중인 소리 연결은 중지됩니다.
        </p>
        <button
          className="button"
          disabled={!state.canResetSettings}
          onClick={() => setResetOpen(true)}
        >
          설정 초기화
        </button>
        {state.settingsMessage && (
          <p className="caption" role="status">
            {state.settingsMessage}
          </p>
        )}
      </section>
      <dialog
        ref={dialog}
        onCancel={() => setResetOpen(false)}
        onClose={() => setResetOpen(false)}
      >
        <div className="dialog-body">
          <h3>설정을 초기화할까요?</h3>
          <p>
            소리 연결이 중지되고 모든 설정이 기본값으로 돌아갑니다. 장치는
            자동으로 다시 선택됩니다.
          </p>
          <div className="dialog-actions">
            <button className="button" onClick={() => setResetOpen(false)}>
              취소
            </button>
            <button
              className="button danger"
              onClick={async () => {
                setResetOpen(false);
                await act("reset");
              }}
            >
              초기화
            </button>
          </div>
        </div>
      </dialog>
    </>
  );
}
function HelpPage() {
  const [error, setError] = useState("");
  return (
    <>
      <div className="page-heading">
        <h2>스테레오 믹스 사용 안내</h2>
        <p className="caption">PC 소리를 다른 앱의 마이크 입력으로 전달하기</p>
      </div>
      <section className="card settings-card">
        <h3>스테레오 믹스 사용을 위한 추가 설정</h3>
        <p>
          이 프로그램으로 PC 소리를 다른 앱의 마이크 입력에 전달하려면,{" "}
          <strong>가상 오디오 케이블을 별도로 설치해야 합니다.</strong>
        </p>
        <p className="caption">
          이미 설치되어 있다면 기존 장치를 사용할 수 있습니다.
        </p>
        <p className="caption">
          VB-CABLE을 사용하거나, 재생 출력을 녹음 입력으로 연결해 주는 다른 가상
          오디오 장치를 사용하세요.
        </p>
      </section>
      <section className="card settings-card">
        <h3>VB-CABLE 기준 설정 방법</h3>
        <ol className="steps">
          <li>
            <span>VB-CABLE을 설치하고 PC를 다시 시작합니다.</span>
            <button
              className="button small"
              onClick={async () => {
                try {
                  await request("openCableWebsite");
                  setError("");
                } catch {
                  setError(
                    "페이지를 열지 못했습니다. 브라우저에서 https://vb-audio.com/Cable/ 에 접속해 주세요.",
                  );
                }
              }}
            >
              설치 페이지
              <Icon name="external" size={14} />
            </button>
          </li>
          <li>
            <span>
              이 프로그램의 <strong>재생 소리</strong>에서 사용할
              헤드폰·스피커를 선택합니다.
            </span>
          </li>
          <li>
            <span>
              <strong>보낼 장치</strong>에서 <b>CABLE Input</b>을 선택하고{" "}
              <strong>소리 연결 시작</strong>을 누릅니다.
            </span>
          </li>
          <li>
            <span>
              Discord·녹음·STT 등 사용할 앱의 <strong>입력 장치</strong>를{" "}
              <b>CABLE Output</b>으로 선택합니다.
            </span>
          </li>
        </ol>
      </section>
      <aside className="flow-note">
        <strong>이 프로그램 → CABLE Input → CABLE Output → 사용할 앱</strong>
        <p className="caption">
          다른 가상 오디오 장치도 해당 제품의 재생·녹음 장치 쌍을 같은 방식으로
          연결하면 됩니다.
        </p>
      </aside>
      {error && (
        <p className="notice" role="alert">
          {error}
        </p>
      )}
    </>
  );
}
