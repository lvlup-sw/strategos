import { z } from "zod"

export const TaskAssignedDataSchema = z.object({ "taskId": z.string(), "title": z.string(), "branch": z.string().optional(), "worktree": z.string().optional(), "assignee": z.string().optional() }).describe("Data payload for `task.assigned` events.")
export type TaskAssignedDataSchema = z.infer<typeof TaskAssignedDataSchema>
