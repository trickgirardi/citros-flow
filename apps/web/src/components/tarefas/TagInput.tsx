"use client"

import { useId, useState } from "react"
import { X } from "lucide-react"

import { TASK_LIMITS, tagStyle } from "@/components/tarefas/task-data"
import { Input } from "@/components/ui/input"
import { cn } from "@/lib/utils"

type TagInputProps = {
  defaultTags: string[]
  /** Visualização: só mostra as tags. */
  readOnly?: boolean
  suggestions: string[]
}

/**
 * Tags em texto livre: digite e tecle Enter ou vírgula para criar o chip. Sugere as já usadas no
 * board. As tags vão no formulário como campos escondidos `tags`.
 */
export function TagInput({
  defaultTags,
  readOnly,
  suggestions,
}: TagInputProps) {
  const [tags, setTags] = useState(defaultTags)
  const [draft, setDraft] = useState("")
  const listId = useId()
  const full = tags.length >= TASK_LIMITS.tags

  function add(raw: string) {
    const tag = raw.trim().slice(0, TASK_LIMITS.tag)

    setDraft("")

    if (!tag || full) {
      return
    }

    setTags((current) =>
      current.some((item) => item.toLowerCase() === tag.toLowerCase())
        ? current
        : [...current, tag]
    )
  }

  return (
    <div className="grid gap-2">
      {tags.length > 0 ? (
        <ul className="flex flex-wrap gap-1.5">
          {tags.map((tag) => (
            <li
              key={tag}
              className={cn(
                "inline-flex items-center gap-1 rounded-pill py-0.5 pr-1 pl-2.5 text-sm font-semibold",
                tagStyle(tag),
                readOnly && "pr-2.5"
              )}
            >
              {tag}
              {readOnly ? null : (
                <button
                  type="button"
                  aria-label={`Remover a tag ${tag}`}
                  onClick={() =>
                    setTags((current) => current.filter((item) => item !== tag))
                  }
                  className="flex size-5 items-center justify-center rounded-pill hover:bg-foreground/10"
                >
                  <X className="size-3.5" />
                </button>
              )}
              <input type="hidden" name="tags" value={tag} />
            </li>
          ))}
        </ul>
      ) : null}

      {readOnly ? (
        tags.length === 0 ? (
          <p className="text-sm text-muted-foreground">Sem tags.</p>
        ) : null
      ) : (
        <>
          <Input
            type="text"
            list={listId}
            value={draft}
            maxLength={TASK_LIMITS.tag}
            disabled={full}
            placeholder={
              full
                ? `Limite de ${TASK_LIMITS.tags} tags`
                : "Digite e tecle Enter"
            }
            aria-label="Adicionar tag"
            className="font-normal"
            onChange={(event) => setDraft(event.target.value)}
            onKeyDown={(event) => {
              // Enter aqui cria o chip; não envia o formulário.
              if (event.key === "Enter" || event.key === ",") {
                event.preventDefault()
                add(draft)
              }
            }}
            onBlur={() => add(draft)}
          />
          <datalist id={listId}>
            {suggestions.map((suggestion) => (
              <option key={suggestion} value={suggestion} />
            ))}
          </datalist>
        </>
      )}
    </div>
  )
}
