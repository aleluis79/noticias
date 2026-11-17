import { vi } from 'vitest'

/** Minimal Audio double: enough to drive the local audio queue in jsdom. */
export class FakeAudio {
  static instances: FakeAudio[] = []

  src = ''
  playbackRate = 1
  preload = ''
  currentTime = 0
  paused = true
  onended: (() => void) | null = null
  onerror: (() => void) | null = null

  play = vi.fn(async () => {
    this.paused = false
  })

  pause = vi.fn(() => {
    this.paused = true
  })

  load = vi.fn()

  removeAttribute = vi.fn((name: string) => {
    if (name === 'src') {
      this.src = ''
    }
  })

  constructor(src = '') {
    this.src = src
    FakeAudio.instances.push(this)
  }

  finish() {
    this.paused = true
    this.onended?.()
  }

  fail() {
    this.onerror?.()
  }

  static reset() {
    FakeAudio.instances = []
  }
}
