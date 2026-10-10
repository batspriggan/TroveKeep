import { get, post, put, del } from './client.js'

const BASE = '/api/users'

export const getAllUsers = () => get(BASE)
export const getUser = (id) => get(`${BASE}/${id}`)
export const createUser = (body) => post(BASE, body)
export const updateUser = (id, body) => put(`${BASE}/${id}`, body)
export const setUserPassword = (id, newPassword) => post(`${BASE}/${id}/password`, { newPassword })
export const deleteUser = (id) => del(`${BASE}/${id}`)
