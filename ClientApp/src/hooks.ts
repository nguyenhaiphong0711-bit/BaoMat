import { useEffect, useRef, useState } from "react";
import { api, ApiError, errorMessage } from "./api";

// Server data belongs to the page. Cancel its request when the URL changes or it unmounts.
export function useRemote<T>(path: string | null, allowNotFound = false) {
  const [version, setVersion] = useState(0);
  const [result, setResult] = useState<{
    path: string | null;
    version: number;
    data: T | null;
    loading: boolean;
    error: string | null;
  }>({ path: null, version: -1, data: null, loading: false, error: null });
  useEffect(() => {
    const controller = new AbortController();
    const update = (data: T | null, error: string | null, loading = false) => {
      if (!controller.signal.aborted)
        setResult({ path, version, data, error, loading });
    };
    update(null, null, !!path);
    if (path)
      api<T>(path, { signal: controller.signal })
        .then((data) => update(data, null))
        .catch((error: unknown) =>
          update(
            null,
            allowNotFound && error instanceof ApiError && error.status === 404
              ? null
              : errorMessage(error),
          ),
        );
    return () => controller.abort();
  }, [path, version, allowNotFound]);
  // Never render data from the previous route while the next effect is waiting to run.
  const current = result.path === path && result.version === version;
  return {
    data: current ? result.data : null,
    error: current ? result.error : null,
    loading: current ? result.loading : !!path,
    reload: () => setVersion((v) => v + 1),
  };
}

// Shared only by forms: one request at a time, with cleanup and a consistent error state.
export function useAction() {
  const request = useRef<AbortController | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  useEffect(
    () => () => {
      request.current?.abort();
    },
    [],
  );
  async function run<T>(
    task: (signal: AbortSignal) => Promise<T>,
    onSuccess: (value: T) => void,
  ) {
    if (request.current) return;
    const controller = new AbortController();
    request.current = controller;
    setBusy(true);
    setError(null);
    try {
      const value = await task(controller.signal);
      if (!controller.signal.aborted) onSuccess(value);
    } catch (error) {
      if (!controller.signal.aborted) setError(errorMessage(error));
    } finally {
      if (!controller.signal.aborted) {
        request.current = null;
        setBusy(false);
      }
    }
  }
  return { busy, error, run };
}
