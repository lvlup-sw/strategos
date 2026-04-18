import { z } from "zod"

export const WorkflowTypeSchema = z.enum(["feature","debug","refactor","oneshot","hotfix","discovery"]).describe("Workflow types supported by the SDLC pipeline.")
export type WorkflowTypeSchema = z.infer<typeof WorkflowTypeSchema>
