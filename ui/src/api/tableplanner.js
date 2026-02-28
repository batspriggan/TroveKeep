import { get, post, put, del } from './client.js'

const TEMPLATES = '/api/table-templates'
const ROOMS = '/api/rooms'

// Templates
export const getAllTemplates = () => get(TEMPLATES)
export const createTemplate = (body) => post(TEMPLATES, body)
export const updateTemplate = (id, body) => put(`${TEMPLATES}/${id}`, body)
export const deleteTemplate = (id) => del(`${TEMPLATES}/${id}`)

// Rooms
export const getAllRooms = () => get(ROOMS)
export const getRoom = (id) => get(`${ROOMS}/${id}`)
export const createRoom = (body) => post(ROOMS, body)
export const updateRoom = (id, body) => put(`${ROOMS}/${id}`, body)
export const saveRoomLayout = (id, layout) => put(`${ROOMS}/${id}/layout`, { layout })
export const deleteRoom = (id) => del(`${ROOMS}/${id}`)
