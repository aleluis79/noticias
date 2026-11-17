export interface FakeVoice {
  name: string
  lang: string
  voiceURI: string
}

export const FAKE_VOICES: FakeVoice[] = [
  { name: 'English', lang: 'en-US', voiceURI: 'en-us' },
  { name: 'España', lang: 'es-ES', voiceURI: 'es-es' },
  { name: 'Latinoamérica', lang: 'es-419', voiceURI: 'es-419' },
  { name: 'Argentina', lang: 'es-AR', voiceURI: 'es-ar' },
]

export class FakeUtterance {
  text: string
  lang = ''
  rate = 1
  voice: FakeVoice | null = null
  onend: ((event: Event) => void) | null = null
  onerror: ((event: { error: string }) => void) | null = null

  constructor(text: string) {
    this.text = text
  }
}

export class FakeSpeechSynthesis {
  voices: FakeVoice[] = [...FAKE_VOICES]
  spoken: FakeUtterance[] = []
  cancelCalls = 0
  pauseCalls = 0
  resumeCalls = 0
  paused = false

  private listeners = new Set<() => void>()

  speak = (utterance: FakeUtterance) => {
    this.spoken.push(utterance)
  }

  cancel = () => {
    this.cancelCalls += 1
    this.paused = false
  }

  pause = () => {
    this.pauseCalls += 1
    this.paused = true
  }

  resume = () => {
    this.resumeCalls += 1
    this.paused = false
  }

  getVoices = () => this.voices

  addEventListener = (type: string, listener: () => void) => {
    if (type === 'voiceschanged') {
      this.listeners.add(listener)
    }
  }

  removeEventListener = (type: string, listener: () => void) => {
    if (type === 'voiceschanged') {
      this.listeners.delete(listener)
    }
  }

  setVoices(voices: FakeVoice[]) {
    this.voices = voices
    for (const listener of this.listeners) {
      listener()
    }
  }

  current(): FakeUtterance | null {
    return this.spoken[this.spoken.length - 1] ?? null
  }

  finish() {
    const utterance = this.current()
    utterance?.onend?.(new Event('end'))
  }

  fail(error: string) {
    const utterance = this.current()
    utterance?.onerror?.({ error })
  }
}

export interface FakeSpeechHandle {
  fake: FakeSpeechSynthesis
  restore: () => void
}

export function installFakeSpeech(voices?: FakeVoice[]): FakeSpeechHandle {
  const fake = new FakeSpeechSynthesis()
  if (voices) {
    fake.voices = voices
  }

  const previousSynthesis = Object.getOwnPropertyDescriptor(
    window,
    'speechSynthesis',
  )
  const previousUtterance = Object.getOwnPropertyDescriptor(
    window,
    'SpeechSynthesisUtterance',
  )

  Object.defineProperty(window, 'speechSynthesis', {
    configurable: true,
    value: fake,
  })
  Object.defineProperty(window, 'SpeechSynthesisUtterance', {
    configurable: true,
    value: FakeUtterance,
  })

  return {
    fake,
    restore() {
      if (previousSynthesis) {
        Object.defineProperty(window, 'speechSynthesis', previousSynthesis)
      } else {
        Reflect.deleteProperty(window, 'speechSynthesis')
      }

      if (previousUtterance) {
        Object.defineProperty(window, 'SpeechSynthesisUtterance', previousUtterance)
      } else {
        Reflect.deleteProperty(window, 'SpeechSynthesisUtterance')
      }
    },
  }
}

export function uninstallSpeech() {
  Reflect.deleteProperty(window, 'speechSynthesis')
  Reflect.deleteProperty(window, 'SpeechSynthesisUtterance')
}
