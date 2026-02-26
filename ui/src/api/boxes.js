import { get, post, put, del } from './client.js'

const BASE = '/api/boxes'

export const getAllBoxes = () => get(BASE)
export const getBox = (id) => get(`${BASE}/${id}`)
export const getBoxContents = (id) => get(`${BASE}/${id}/contents`)
export const createBox = (body) => post(BASE, body)
export const updateBox = (id, body) => put(`${BASE}/${id}`, body)
export const deleteBox = (id) => del(`${BASE}/${id}`)
