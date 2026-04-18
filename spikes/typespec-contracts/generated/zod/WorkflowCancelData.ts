import { z } from "zod"

export const WorkflowCancelDataSchema = z.object({ "from": z.string(), "to": z.string(), "trigger": z.string(), "featureId": z.string(), "reason": z.string().optional() }).describe("Data payload for `workflow.cancel` events.")
export type WorkflowCancelDataSchema = z.infer<typeof WorkflowCancelDataSchema>
